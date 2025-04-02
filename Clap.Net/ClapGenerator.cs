using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Clap.Net;

internal record CommandModel(
    string Kind,
    string? Name,
    string? About,
    string? LongAbout,
    string? Version,
    INamedTypeSymbol Symbol,
    SubCommandArgumentModel? SubCommandArgumentModel,
    ArgumentModel[] Arguments
);

internal record SubCommandModel(ISymbol Symbol, CommandModel[] Commands);
internal record SubCommandArgumentModel(ISymbol Symbol, ITypeSymbol MemberType, string Name);
internal abstract record ArgumentModel(ISymbol Symbol, ITypeSymbol MemberType, string VariableName);
internal record PositionalArgumentModel(
    ISymbol Symbol,
    AttributeData? Attribute,
    ITypeSymbol MemberType,
    string VariableName,
    int Index,
    bool Required = false
) : ArgumentModel(Symbol, MemberType, VariableName);

internal record NamedArgumentModel(
    ISymbol Symbol,
    AttributeData? Attribute,
    string VariableName,
    string? Help,
    char? Short,
    string? Long,
    string? Env,
    ITypeSymbol MemberType
) : ArgumentModel(Symbol, MemberType, VariableName);

[Generator]
public class ClapGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat TypeNameFormat =
        new(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilerInfoProvider = context
            .CompilationProvider
            .Select((c, _) =>
            {
                return c.Assembly.Identity.Version.ToString();
            });

        var commandCandidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, _) =>
                {
                    var decl = (TypeDeclarationSyntax)ctx.Node;
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(decl)!;
                    return GetCommandModel(decl, symbol);
                }
            )
            .Where(symbol => symbol != null);

        var subCommandCandidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, _) =>
                {
                    var decl = (TypeDeclarationSyntax)ctx.Node;
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(decl);

                    if (symbol is null)
                        return null;

                    var subCommandAttribute = symbol
                        .GetAttributes()
                        .FirstOrDefault(attr => attr.AttributeClass?.Name is nameof(SubCommandAttribute));

                    if (subCommandAttribute is null)
                        return null;

                    var commands = new List<CommandModel>();
                    var namedTypeSymbol = (INamedTypeSymbol)symbol;
                    foreach (var nestedTypeSymbol in namedTypeSymbol.GetTypeMembers())
                    {
                        if (nestedTypeSymbol.DeclaredAccessibility != Accessibility.Public)
                            continue;

                        // get the syntax of the nested symbol
                        var typeDeclarationSyntax = nestedTypeSymbol.DeclaringSyntaxReferences
                            .OfType<TypeDeclarationSyntax>()
                            .FirstOrDefault();

                        if (GetCommandModel(typeDeclarationSyntax, nestedTypeSymbol) is { } model)
                            commands.Add(model);
                    }

                    return new SubCommandModel(symbol, [.. commands]);
                }
            )
            .Where(symbol => symbol != null);

        var combined = commandCandidates.Collect()
            .Combine(subCommandCandidates.Collect())
            .Combine(compilerInfoProvider);

        context.RegisterSourceOutput(combined, (spc, combined) =>
        {
            var ((commandModels, subCommandModels), version) = combined!;

            foreach (var commandModel in commandModels)
            {
                if (commandModel is null)
                    continue;

                var output = GenerateCommandParseMethod(commandModel, subCommandModels, version);
                spc.AddSource($"{commandModel.Symbol.Name}.ParseMethod.g.cs", SourceText.From(output, Encoding.UTF8));
            }

            foreach (var subCommandModel in subCommandModels)
            {
                if (subCommandModel is null)
                    continue;

                var output = GenerateSubCommandParseMethod(subCommandModel);
                spc.AddSource($"{subCommandModel.Symbol.Name}.ParseMethod.g.cs", SourceText.From(output, Encoding.UTF8));
            }
        });
    }

    private static CommandModel? GetCommandModel(TypeDeclarationSyntax? typeDeclarationSyntax, ISymbol commandCandidateSymbol)
    {
        var commandAttribute = commandCandidateSymbol
            .GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name is nameof(CommandAttribute));

        if (commandAttribute is null)
            return null;

        var namedTypeSymbol = (INamedTypeSymbol)commandCandidateSymbol;
        var arguments = new List<ArgumentModel>();
        var subCommandArgumentModel = (SubCommandArgumentModel?)null;

        var positionalIndex = 0;
        foreach (var member in namedTypeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol and not IFieldSymbol || member.DeclaredAccessibility != Accessibility.Public)
                continue;

            var memberType = member switch
            {
                IPropertySymbol prop => prop.Type,
                IFieldSymbol field => field.Type,
                _ => null
            };

            if (memberType is null)
                continue;

            var attributes = member
                .GetAttributes()
                .Where(attr => attr.AttributeClass?.Name is not null)
                .ToDictionary(attr => attr.AttributeClass?.Name!);

            if (attributes.TryGetValue(nameof(CommandAttribute), out var command))
            {
                var name = command?.NamedArguments.FirstOrDefault(a => a.Key is nameof(CommandAttribute.Name)).Value.Value?.ToString() ?? member.Name;
                subCommandArgumentModel = new SubCommandArgumentModel(member, memberType, name);
                continue;
            }

            var variableName = GetVariableName(member.Name.ToCamelCase());

            if (attributes.TryGetValue(nameof(ArgAttribute), out var argAttribute) is false)
            {
                arguments.Add(new PositionalArgumentModel(member, null, memberType, variableName, positionalIndex++));
                continue;
            }

            var help = argAttribute.NamedArguments.FirstOrDefault(a => a.Key is nameof(ArgAttribute.Help)).Value.Value;
            var @short = argAttribute.NamedArguments.FirstOrDefault(a => a.Key is nameof(ArgAttribute.Short)).Value.Value;
            var @long = argAttribute.NamedArguments.FirstOrDefault(a => a.Key is nameof(ArgAttribute.Long)).Value.Value;
            var env = argAttribute.NamedArguments.FirstOrDefault(a => a.Key is nameof(ArgAttribute.Env)).Value.Value;

            if (@short is not null || @long is not null)
            {
                arguments.Add(new NamedArgumentModel(
                    member,
                    argAttribute,
                    variableName,
                    help as string,
                    @short as char?,
                    @long as string,
                    env as string,
                    memberType
                ));
                continue;
            }

            arguments.Add(new PositionalArgumentModel(member, null, memberType, variableName, positionalIndex++));
        }

        var about = commandCandidateSymbol.GetDocumentationCommentXml();

        if (typeDeclarationSyntax is StructDeclarationSyntax ss)
        {
            var attributeLists = ss.AttributeLists;
        }

        return new CommandModel(
            Kind: commandCandidateSymbol switch
            {
                INamedTypeSymbol { TypeKind: TypeKind.Class } @class => @class.IsRecord ? "record" : "class",
                INamedTypeSymbol { TypeKind: TypeKind.Struct } @struct => @struct.IsRecord ? "record struct" : "struct",
                _ => "class"
            },
            Name: commandAttribute.NamedArguments.FirstOrDefault(a => a.Key is nameof(CommandAttribute.Name)).Value.Value?.ToString() ?? commandCandidateSymbol.Name.ToSnakeCase(),
            About: about,
            LongAbout: commandAttribute.NamedArguments.FirstOrDefault(a => a.Key is nameof(CommandAttribute.LongAbout)).Value.Value?.ToString(),
            Version: commandAttribute.NamedArguments.FirstOrDefault(a => a.Key is nameof(CommandAttribute.Version)).Value.Value?.ToString(),
            Symbol: namedTypeSymbol,
            SubCommandArgumentModel: subCommandArgumentModel,
            Arguments: [.. arguments]
        );
    }

    private static string GenerateCommandParseMethod(
        CommandModel commandModel,
        ImmutableArray<SubCommandModel?> subCommandModels,
        string? assemblyVersion)
    {
        var typeName = commandModel.Symbol.Name;
        var fullName = commandModel.Symbol.ToDisplayString(TypeNameFormat);
        var subCommand = commandModel.SubCommandArgumentModel is { } c
                ? FindSubCommand(c.MemberType, subCommandModels)
                : null;
        var subCommands = subCommand?.Commands ?? [];
        var version = commandModel.Version ?? assemblyVersion;

        var ns = commandModel.Symbol.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : commandModel.Symbol.ContainingNamespace.ToDisplayString();

        using var ms = new MemoryStream();
        var writer = new Utf8IndentedWriter(ms);

        writer.WriteLine("#nullable enable");
        writer.WriteLine();

        if (ns is not null)
        {
            writer.WriteLine($"namespace {ns};\n");
            writer.WriteLine();
        }

        if (commandModel.Symbol.ContainingType is not null)
        {
            writer.WriteLine($"public partial class {commandModel.Symbol.ContainingType.Name}");
            writer.WriteLine("{");
            writer.IncreaseIndent();
        }

        writer.WriteLine($$"""
                           public partial {{commandModel.Kind}} {{typeName}}
                           {
                           """);

        writer.IncreaseIndent();

        writer.WriteLine($"private const string HelpMessage = ");
        writer.IncreaseIndent();
        writer.WriteLine("\"\"\"");
        writer.WriteLine(GenerateHelpMessage(commandModel, subCommand, version));
        writer.WriteLine("\"\"\";");
        writer.DecreaseIndent();
        writer.WriteLine();

        writer.WriteLine($$"""
                           public static {{fullName}} Parse(System.ReadOnlySpan<string> args)
                           {
                           """);

        writer.IncreaseIndent();

        writer.WriteLine("""
                         if (args.Length > 0 && args[0] is "-h" or "--help") 
                         {
                             PrintHelpMessage();
                             System.Environment.Exit(0);
                         }
                         """);
        writer.WriteLine();

        if (version is not null)
        {
            writer.WriteLine($$"""
                               if (args.Length > 0 && args[0] is "-v" or "--version")
                               {
                                   System.Console.WriteLine("{{version}}");
                                   System.Environment.Exit(0);
                               }
                               """);
            writer.WriteLine();
        }

        if (commandModel.Arguments.Length is 0)
        {
            writer.WriteLine($"return new {fullName}();");
            writer.DecreaseAndWriteLine("}");
            WriteHelperMethods(writer);
            writer.DecreaseAndWriteLine("}");

            if (commandModel.Symbol.ContainingType is not null)
                writer.DecreaseAndWriteLine("}");

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        foreach (var arg in commandModel.Arguments.OfType<NamedArgumentModel>())
        {
            var type = arg.MemberType.ToDisplayString(TypeNameFormat);
            var initialValue = GetDefaultValueString(arg.MemberType);
            var defaultValue = arg.Env is { } env
                ? $"Environment.GetEnvironmentVariable(\"{env}\") is {{ }} env ? {GetArgConversion(arg.MemberType, "env")} : {initialValue}"
                : initialValue;

            writer.WriteLine($"// Argument '{arg.Symbol.Name}' is a named argument");
            writer.WriteLine($"{type} {arg.VariableName} = {defaultValue};");
        }

        foreach (var arg in commandModel.Arguments.OfType<PositionalArgumentModel>())
        {
            var type = arg.MemberType.ToDisplayString(TypeNameFormat);
            var defaultValue = GetDefaultValueString(arg.MemberType);

            writer.WriteLine($"// Argument '{arg.Symbol.Name}' is positional argument at index {arg.Index}");
            writer.WriteLine($"{type} {arg.VariableName} = {defaultValue};");
        }

        writer.WriteLine();

        var positionalArgs = commandModel.Arguments.OfType<PositionalArgumentModel>().ToArray();
        if (positionalArgs.Length > 0) writer.WriteLine("var positionalIndex = 0;");
        var lastIndex = positionalArgs.Length > 0 ? positionalArgs.Max(a => a.Index) : -1;

        writer.WriteLine("""
                         var index = 0;
                         while (index < args.Length)
                         {
                             switch (args[index])
                             {
                         """);

        writer.IncreaseIndent(by: 2);

        if (subCommand is not null)
        {
            writer.WriteLine($"// Setting subcommand '{subCommand.Symbol.ToDisplayString()}'");
            writer.WriteLine("// Command switching swallows the rest of the arguments but previously");
            writer.WriteLine("// parsed arguments still get applied to the model");

            var names = new string[subCommand.Commands.Length];
            for (var i = 0; i < subCommand.Commands.Length; i++)
                names[i] = $"\"{subCommand.Commands[i].Name}\"";

            writer.WriteLine($"case {string.Join(" or ", names)}:");
            writer.IncreaseIndent();
            writer.WriteLine($"return new {fullName}");
            writer.WriteLine('{');
            writer.IncreaseIndent();
            writer.WriteLine($"{commandModel.SubCommandArgumentModel!.Symbol.Name} = {subCommand.Symbol.ToDisplayString(TypeNameFormat)}.Resolve(args[index..]),");

            foreach (var arg in commandModel.Arguments)
                writer.WriteLine($"{arg.Symbol.Name} = {arg.VariableName},");

            writer.DecreaseIndent();
            writer.WriteLine("};");
            writer.DecreaseIndent();
            writer.WriteLine();
        }

        foreach (var argument in commandModel.Arguments)
        {
            switch (argument)
            {
                case NamedArgumentModel value:
                    {
                        var name = value switch
                        {
                            { Short: { } shortName, Long: { } longName } => $"\"-{shortName}\" or \"--{longName}\"",
                            { Short: { } shortName } => $"\"-{shortName}\"",
                            { Long: { } longName } => $"\"--{longName}\"",
                            _ => $"\"{value.Symbol.Name.ToSnakeCase()}\""
                        };

                        writer.WriteLine($"// Setting attribute '{fullName}.{value.Symbol.Name}'");
                        writer.WriteLine($"case {name}:");
                        writer.WriteLine("{");
                        writer.IncreaseIndent();
                        writer.WriteLine("index++;");
                        SetNamedArgumentValue(writer, value);
                        writer.WriteLine("break;");
                        writer.DecreaseIndent();
                        writer.WriteLine("}");
                        writer.WriteLine();
                        break;
                    }

                case PositionalArgumentModel positional:
                    {
                        writer.WriteLine($"// Setting attribute '{fullName}.{positional.Symbol.Name}'");
                        writer.WriteLine($"case var arg when !arg.StartsWith('-') && positionalIndex is {positional.Index}:");
                        writer.WriteLine("{");
                        writer.IncreaseIndent();
                        SetPositionalValue(writer, positional);
                        writer.WriteLine("positionalIndex++;");
                        writer.WriteLine("break;");
                        writer.DecreaseIndent();
                        writer.WriteLine("}");
                        writer.WriteLine();
                        break;
                    }
            }
        }

        writer.WriteLine("""
                         case var arg:
                             DisplayError($"Unknown argument '{arg}'");
                             break;
                         """);


        writer.DecreaseAndWriteLine("}");
        writer.WriteLine();
        writer.DecreaseAndWriteLine("}");

        writer.WriteLine();

        if (commandModel.SubCommandArgumentModel is { MemberType.NullableAnnotation: not NullableAnnotation.Annotated } reqCommand)
        {
            writer.WriteLine($"""
                              DisplayError("SubCommand '{reqCommand.Name}' is required");
                              return default!;
                              """);
        }
        else
        {

            writer.WriteLine("""
                             
                             """);

            var requiredArguments = commandModel.Arguments
                .OfType<PositionalArgumentModel>()
                .Where(a => a.Required || a.MemberType.NullableAnnotation is not NullableAnnotation.Annotated)
                .ToArray();

            if (requiredArguments.Length > 0)
            {
                writer.WriteLine($$"""
                                if ({{string.Join(" || ", requiredArguments.Select(a => $"{a.VariableName} == default"))}})
                                {
                                    var sb = new System.Text.StringBuilder();
                                """);
                writer.IncreaseIndent();

                foreach (var requiredArg in requiredArguments)
                {
                    switch (requiredArg)
                    {
                        case IArrayTypeSymbol arrayTypeSymbol:
                            {
                                writer.WriteLine($$"""
                                            if ({{requiredArg.VariableName}}.Length is 0)
                                                sb.AppendLine("  <{{requiredArg.Symbol.Name}}>");
                                            """);
                                break;
                            }

                        default:
                            {
                                writer.WriteLine($$"""
                                            if ({{requiredArg.VariableName}} == default)
                                                sb.AppendLine("  <{{requiredArg.Symbol.Name}}>");
                                            """);
                                break;
                            }
                    }
                }

                writer.WriteLine(@"DisplayError($""The following required arguments were not provided:\n{sb.ToString()}"");");
                writer.DecreaseIndent();
                writer.WriteLine("}");
            }

            writer.WriteLine();
            writer.WriteLine($"return new {fullName}");
            writer.WriteLine('{');
            writer.IncreaseIndent();

            foreach (var arg in commandModel.Arguments)
            {
                var variableName = GetVariableName(arg.Symbol.Name.ToCamelCase());
                writer.WriteLine($"{arg.Symbol.Name} = {variableName},");
            }

            writer.DecreaseIndent();
            writer.WriteLine("};");
        }

        writer.DecreaseAndWriteLine("}"); // This closes the Parse method
        WriteHelperMethods(writer);
        writer.DecreaseAndWriteLine("}"); // This closes the Class/Struct body

        if (commandModel.Symbol.ContainingType is not null)
            writer.DecreaseAndWriteLine("}");

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string GenerateSubCommandParseMethod(SubCommandModel subCommandModel)
    {
        var (symbol, commands) = subCommandModel;
        var typeName = symbol.Name;
        var fullName = symbol.ToDisplayString(TypeNameFormat);
        var ns = symbol.ContainingNamespace.IsGlobalNamespace
                    ? null
                    : symbol.ContainingNamespace.ToDisplayString();

        using var ms = new MemoryStream();
        var writer = new Utf8IndentedWriter(ms);

        writer.WriteLine("#nullable enable");
        writer.WriteLine();

        if (ns is not null)
        {
            writer.WriteLine($"namespace {ns};\n");
            writer.WriteLine();
        }

        writer.WriteLine(
            $$"""
              public partial class {{typeName}}
              {
                  public static {{fullName}} Resolve(System.ReadOnlySpan<string> args)
                  {
                      // {{commands.Length}} commands to parse
                      return args[0] switch 
                      {
              """);

        writer.IncreaseIndent(by: 3);

        foreach (var command in commands)
        {
            var name = command.Symbol.GetAttributes()
                .First(attr => attr.AttributeClass?.Name is nameof(CommandAttribute))
                .NamedArguments.FirstOrDefault(a => a.Key is nameof(CommandAttribute.Name)).Value.Value?.ToString()
                ?? command.Symbol.Name.ToSnakeCase();

            var commandFullName = command.Symbol.ToDisplayString(TypeNameFormat);
            writer.WriteLine($"\"{name}\" => {commandFullName}.Parse(args[1..]),");
        }

        writer.WriteLine("_ => throw new Exception(\"Unknown command\")");

        writer.DecreaseAndWriteLine("};");
        writer.DecreaseAndWriteLine("}");
        writer.DecreaseAndWriteLine("}");

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void SetPositionalValue(Utf8IndentedWriter writer, PositionalArgumentModel argument)
    {
        // If member is array
        if (argument.MemberType is IArrayTypeSymbol { ElementType: { } elementType })
        {
            WriteArraySetter(writer, argument.VariableName, elementType);
            return;
        }

        writer.WriteLine($"""
                         {argument.VariableName} = {GetArgConversion(argument.MemberType)};
                         index++;
                         """);
    }

    private static void SetNamedArgumentValue(Utf8IndentedWriter writer, NamedArgumentModel argument)
    {
        if (argument.MemberType.ToDisplayString(TypeNameFormat) is "System.Boolean" or "System.Boolean?")
        {
            writer.WriteLine($$"""
                               if (index < args.Length 
                                    && !args[index].StartsWith('-') 
                                    && bool.TryParse(args[index + 1], out var b)) 
                               {
                                   {{argument.VariableName}} = b;
                                   index++;
                                   break;
                               }
          
                               // If a value is not given then we should treat this has a positive flag
                               {{argument.VariableName}} = true;
                               """);
            return;
        }

        // If member is array
        if (argument.MemberType is IArrayTypeSymbol { ElementType: { } elementType })
        {
            WriteArraySetter(writer, argument.VariableName, elementType);
            return;
        }

        writer.WriteLine($"""
                         {argument.VariableName} = {GetArgConversion(argument.MemberType)};
                         index++;
                         """);
    }

    private static void WriteArraySetter(Utf8IndentedWriter writer, string variableName, ITypeSymbol elementType)
    {
        var childType = elementType.ToDisplayString(TypeNameFormat);
        writer.WriteLine($$"""
                               var builder = System.Collections.Immutable.ImmutableArray.CreateBuilder<{{childType}}>();
                               while (index < args.Length && !args[index].StartsWith('-')) 
                               {
                                   builder.Add({{GetArgConversion(elementType)}});
                                   index++;
                               }
                               {{variableName}} = builder.ToArray();
                               """);
    }

    private static string? GetArgConversion(ITypeSymbol member, string variableName = "args[index]")
    {
        var nullable = member.NullableAnnotation is NullableAnnotation.Annotated;
        return member.ToDisplayString(TypeNameFormat).TrimEnd('?') switch
        {
            "System.String" => variableName,
            "System.Int32" => nullable ? $"int.TryParse({variableName}, out var v) ? v : null" : $"int.Parse({variableName})",
            "System.Int64" => nullable ? $"long.TryParse({variableName}, out var v) ? v : null" : $"long.Parse({variableName})",
            "System.Single" => nullable ? $"float.TryParse({variableName}, out var v) ? v : null" : $"float.Parse({variableName})",
            "System.Double" => nullable ? $"double.TryParse({variableName}, out var v) ? v : null" : $"double.Parse({variableName})",
            "System.Decimal" => nullable ? $"decimal.TryParse({variableName}, out var v) ? v : null" : $"decimal.Parse({variableName})",
            "System.Boolean" => nullable ? $"bool.TryParse({variableName}, out var v) ? v : null" : $"bool.Parse({variableName})",
            "System.Byte" => nullable ? $"byte.TryParse({variableName}, out var v) ? v : null" : $"byte.Parse({variableName})",
            "System.SByte" => nullable ? $"sbyte.TryParse({variableName}, out var v) ? v : null" : $"sbyte.Parse({variableName})",
            "System.Int16" => nullable ? $"short.TryParse({variableName}, out var v) ? v : null" : $"short.Parse({variableName})",
            "System.UInt16" => nullable ? $"ushort.TryParse({variableName}, out var v) ? v : null" : $"ushort.Parse({variableName})",
            "System.UInt32" => nullable ? $"uint.TryParse({variableName}, out var v) ? v : null" : $"uint.Parse({variableName})",
            "System.UInt64" => nullable ? $"ulong.TryParse({variableName}, out var v) ? v : null" : $"ulong.Parse({variableName})",
            "System.Char" => nullable ? $"char.TryParse({variableName}, out var v) ? v : null" : $"char.Parse({variableName})",
            "System.DateTime" => nullable ? $"DateTime.TryParse({variableName}, out var v) ? v : null" : $"DateTime.Parse({variableName})",
            "System.TimeSpan" => nullable ? $"TimeSpan.TryParse({variableName}, out var v) ? v : null" : $"TimeSpan.Parse({variableName})",
            "System.Guid" => nullable ? $"Guid.TryParse({variableName}, out var v) ? v : null" : $"Guid.Parse({variableName})",
            var other => $"Convert.ChangeType({variableName}, typeof({(nullable ? $"{other}?" : other)}))"
        };
    }

    private static string GetVariableName(string name) => name switch
    {
        "params" => "@params",
        "base" => "@base",
        "this" => "@this",
        "default" => "@default",
        "event" => "@event",
        "field" => "@field",
        "var" => "@var",
        _ => name
    };

    private static string GetDefaultValueString(ITypeSymbol memberType) => memberType switch
    {
        { NullableAnnotation: NullableAnnotation.Annotated } => "null",
        IArrayTypeSymbol => $"[]",
        _ => "default!"
    };

    private static SubCommandModel? FindSubCommand(ITypeSymbol member, ImmutableArray<SubCommandModel?> subCommandModels)
    {
        var memberType = member.ToDisplayString(TypeNameFormat).TrimEnd('?');
        return subCommandModels.FirstOrDefault(sc => memberType == sc?.Symbol.ToDisplayString(TypeNameFormat).TrimEnd('?'));
    }

    private static string GenerateHelpMessage(CommandModel commandModel, SubCommandModel? subCommand, string? version)
    {
        var commandName = commandModel.Name;
        var sb = new StringBuilder();


        sb.AppendLine($"About {commandModel.About!.Trim()}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(commandModel.LongAbout))
        {
            sb.AppendLine(commandModel.LongAbout);
            sb.AppendLine();
        }

        sb.Append("Usage: {{EXECUTABLE_NAME}}");

        if (commandModel.Arguments.OfType<NamedArgumentModel>().Any())
            sb.Append(" [OPTIONS]");

        foreach (var positional in commandModel.Arguments.OfType<PositionalArgumentModel>())
            sb.Append($" {positional.Symbol.Name.ToSnakeCase()}");

        sb.AppendLine();
        sb.AppendLine();

        var table = new List<string?[]>();
        foreach (var option in commandModel.Arguments.OfType<NamedArgumentModel>())
        {
            var names = option switch
            {
                { Short: { } shortName, Long: { } longName } => $"-{shortName}, --{longName}",
                { Short: { } shortName } => $"-{shortName}",
                { Long: { } longName } => $"--{longName}",
                _ => $"{option.Symbol.Name.ToSnakeCase()}"
            };

            table.Add([names, option.Help]);
        }

        table.Add(["-h, --help", "Shows this help message"]);

        var maxColumnLength = table.Max(o => o[0]?.Length ?? 0);

        sb.AppendLine("Options:");
        foreach (var row in table)
            sb.AppendLine($"  {row[0]?.PadRight(maxColumnLength)}  {row[1]}");
        sb.AppendLine();

        if (subCommand is { Commands.Length: > 0 })
        {
            table.Clear();
            foreach (var command in subCommand.Commands)
                table.Add([command.Name, command.About]);

            maxColumnLength = table.Max(o => o[0]?.Length ?? 0);

            sb.AppendLine("Commands:");
            foreach (var row in table)
                sb.AppendLine($"  {row[0]?.PadRight(maxColumnLength)}  {row[1]}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void WriteHelperMethods(Utf8IndentedWriter writer)
    {
        writer.WriteLine();
        writer.WriteLine("""
                         [System.Diagnostics.CodeAnalysis.DoesNotReturn]
                         public static void DisplayError(string message)
                         {
                             var previousColour = System.Console.ForegroundColor;
                             System.Console.ForegroundColor = System.ConsoleColor.Red;
                             System.Console.WriteLine($"{message}\n");
                             System.Console.ForegroundColor = previousColour;
                             PrintHelpMessage();
                             System.Environment.Exit(0);
                         }

                         private static void PrintHelpMessage() 
                         {
                             var executableName = System.IO.Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);
                             if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                                 executableName += "[.exe]";
                             System.Console.WriteLine(HelpMessage.Replace("{{EXECUTABLE_NAME}}", executableName));
                         }
                         """);
    }

    private static string? GetDocComment(TypeDeclarationSyntax syntax)
    {
        var leadingTrivia = syntax.GetLeadingTrivia();

        var docCommentTrivia = leadingTrivia
            .Select(trivia => trivia.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .FirstOrDefault();

        if (docCommentTrivia == null)
            return null;

        var summary = docCommentTrivia.Content
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(e => e.StartTag.Name.ToString() == "summary");

        if (summary is null)
            return null;

        var content = string.Concat(summary.Content
            .OfType<XmlTextSyntax>()
            .SelectMany(t => t.TextTokens)
            .Select(t => t.Text)).Trim();

        return RemoveInvalidCharacters(content.AsSpan());
    }

    private static readonly char[] AllowedCharacters = [' ', '\t', '\n', '\r', '&'];

    private static string RemoveInvalidCharacters(ReadOnlySpan<char> source)
    {
        Span<char> span = stackalloc char[source.Length];
        var index = 0;
        var spanIndex = 0;
        while (index < source.Length)
        {
            var c = source[index];
            if (char.IsLetterOrDigit(c) || AllowedCharacters.Contains(c))
                span[spanIndex++] = c;

            index++;
        }

        return span.Slice(0, spanIndex).ToString();
    }
}