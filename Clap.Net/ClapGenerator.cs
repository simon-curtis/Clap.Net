using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Clap.Net;

internal record CommandModel(
    string Kind,
    string? Name,
    string? Summary,
    string? Description,
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
    bool Required = false,
    bool Last = false
) : ArgumentModel(Symbol, MemberType, VariableName);

internal record NamedArgumentModel(
    ISymbol Symbol,
    AttributeData? Attribute,
    string VariableName,
    string? Description,
    char? ShortName,
    string? LongName,
    ITypeSymbol MemberType,
    bool Required
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
        var commandCandidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                transform: static (ctx, _) =>
                {
                    var decl = (TypeDeclarationSyntax)ctx.Node;
                    return ctx.SemanticModel.GetDeclaredSymbol(decl) is { } symbol ? GetCommandModel(symbol) : null;
                }
            )
            .Where(symbol => symbol != null);

        var subCommandCandidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
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
                    foreach (var nestedType in namedTypeSymbol.GetTypeMembers())
                    {
                        if (nestedType.DeclaredAccessibility != Accessibility.Public)
                            continue;

                        if (GetCommandModel(nestedType) is { } model)
                            commands.Add(model);
                    }

                    return new SubCommandModel(symbol, [.. commands]);
                }
            )
            .Where(symbol => symbol != null);

        var combined = commandCandidates.Collect().Combine(subCommandCandidates.Collect());

        context.RegisterSourceOutput(combined, (spc, combined) =>
        {
            var (commandModels, subCommandModels) = combined!;

            foreach (var commandModel in commandModels)
            {
                if (commandModel is null)
                    continue;

                var output = GenerateCommandParseMethod(commandModel, subCommandModels);
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

    private static CommandModel? GetCommandModel(ISymbol symbol)
    {
        var commandAttribute = symbol
            .GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name is nameof(CommandAttribute));

        if (commandAttribute is null)
            return null;

        var namedTypeSymbol = (INamedTypeSymbol)symbol;
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

            var description = argAttribute.NamedArguments.FirstOrDefault(a => a.Key is nameof(ArgAttribute.Description)).Value.Value;
            var shortName = argAttribute.NamedArguments.FirstOrDefault(a => a.Key is nameof(ArgAttribute.ShortName)).Value.Value;
            var longName = argAttribute.NamedArguments.FirstOrDefault(a => a.Key is nameof(ArgAttribute.LongName)).Value.Value;
            var required = argAttribute.NamedArguments.FirstOrDefault(a => a.Key is nameof(ArgAttribute.Required)).Value.Value;

            if (shortName is not null || longName is not null)
            {
                arguments.Add(new NamedArgumentModel(
                    member,
                    argAttribute,
                    variableName,
                    description as string,
                    shortName as char?,
                    longName as string,
                    memberType,
                    required as bool? ?? false
                ));
                continue;
            }

            if (argAttribute.NamedArguments.FirstOrDefault(a => a.Key is nameof(ArgAttribute.Index)).Value.Value is { } index)
            {
                arguments.Add(new PositionalArgumentModel(member, null, memberType, variableName, int.Parse(index.ToString())));
                continue;
            }

            if (argAttribute.NamedArguments.Any(a => a.Key is nameof(ArgAttribute.Last)))
            {
                arguments.Add(new PositionalArgumentModel(member, null, memberType, variableName, 0, true));
                continue;
            }

            arguments.Add(new PositionalArgumentModel(member, null, memberType, variableName, positionalIndex++));
        }

        return new CommandModel(
            symbol switch
            {
                INamedTypeSymbol { TypeKind: TypeKind.Class } @class => @class.IsRecord ? "record" : "class",
                INamedTypeSymbol { TypeKind: TypeKind.Struct } @struct => @struct.IsRecord ? "record struct" : "struct",
                _ => "class"
            },
            commandAttribute.NamedArguments.FirstOrDefault(a => a.Key is nameof(CommandAttribute.Name)).Value.Value?.ToString() ?? symbol.Name.ToSnakeCase(),
            commandAttribute.NamedArguments.FirstOrDefault(a => a.Key is nameof(CommandAttribute.Summary)).Value.Value?.ToString(),
            commandAttribute.NamedArguments.FirstOrDefault(a => a.Key is nameof(CommandAttribute.Description)).Value.Value?.ToString(),
            namedTypeSymbol,
            subCommandArgumentModel,
            [.. arguments]
        );
    }

    private static string GenerateCommandParseMethod(CommandModel commandModel, ImmutableArray<SubCommandModel?> subCommandModels)
    {
        var typeName = commandModel.Symbol.Name;
        var fullName = commandModel.Symbol.ToDisplayString(TypeNameFormat);
        var subCommand = commandModel.SubCommandArgumentModel is { } c
                ? FindSubCommand(c.MemberType, subCommandModels)
                : null;
        var subCommands = subCommand?.Commands ?? [];

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

        writer.WriteLine($"private const string HelpMessage = \"\"\"");
        writer.IncreaseIndent();
        writer.WriteLine(GenerateHelpMessage(commandModel, subCommand));
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

        foreach (var argument in commandModel.Arguments.OrderBy(a => a is not PositionalArgumentModel m || m.Last))
        {
            switch (argument)
            {
                case PositionalArgumentModel arg:
                    {
                        var type = arg.MemberType.ToDisplayString(TypeNameFormat);
                        var defaultValue = GetDefaultValueString(arg.MemberType);

                        var indexDescription = arg.Last ? "and is last" : $"at index {arg.Index}";
                        writer.WriteLine($"// Argument '{arg.Symbol.Name}' is positional argument {indexDescription}");
                        writer.WriteLine($"{type} {arg.VariableName} = {defaultValue};");
                        break;
                    }

                case NamedArgumentModel arg:
                    {
                        var type = arg.MemberType.ToDisplayString(TypeNameFormat);
                        var defaultValue = GetDefaultValueString(arg.MemberType);

                        writer.WriteLine($"// Argument '{arg.Symbol.Name}' is a named argument");
                        writer.WriteLine($"{type} {arg.VariableName} = {defaultValue};");
                        break;
                    }
            }
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
                            { ShortName: { } shortName, LongName: { } longName } => $"\"-{shortName}\" or \"--{longName}\"",
                            { ShortName: { } shortName } => $"\"-{shortName}\"",
                            { LongName: { } longName } => $"\"--{longName}\"",
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
                        writer.WriteLine($"case var arg when !arg.StartsWith('-') && positionalIndex is {(positional.Last ? (lastIndex + 1) : positional.Index)}:");
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

            if (commandModel.Arguments
                .OfType<PositionalArgumentModel>()
                .Where(a => a.Required || a.MemberType.NullableAnnotation is not NullableAnnotation.Annotated)
                .ToArray() is { Length: > 0 } requiredArguments)
            {
                foreach (var requiredArg in requiredArguments)
                {
                    switch (requiredArg)
                    {
                        case IArrayTypeSymbol arrayTypeSymbol:
                            {
                                writer.WriteLine($$"""
                                            if ({{requiredArg.VariableName}}.Length is 0)
                                                DisplayError($"Positional array '{{requiredArg.VariableName}}' is required");
                                            """);
                                break;
                            }

                        default:
                            {
                                writer.WriteLine($$"""
                                            if ({{requiredArg.VariableName}} == default)
                                                DisplayError($"Positional array '{{requiredArg.VariableName}}' is required");
                                            """);
                                break;
                            }
                    }
                }

                writer.WriteLine();
            }

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

    private static string? GetArgConversion(ITypeSymbol member)
    {
        var nullable = member.NullableAnnotation is NullableAnnotation.Annotated;
        return member.ToDisplayString(TypeNameFormat).TrimEnd('?') switch
        {
            "System.String" => "args[index]",
            "System.Int32" => nullable ? $"int.TryParse(args[index], out var v) ? v : null" : $"int.Parse(args[index])",
            "System.Int64" => nullable ? $"long.TryParse(args[index], out var v) ? v : null" : $"long.Parse(args[index])",
            "System.Single" => nullable ? $"float.TryParse(args[index], out var v) ? v : null" : $"float.Parse(args[index])",
            "System.Double" => nullable ? $"double.TryParse(args[index], out var v) ? v : null" : $"double.Parse(args[index])",
            "System.Decimal" => nullable ? $"decimal.TryParse(args[index], out var v) ? v : null" : $"decimal.Parse(args[index])",
            "System.Boolean" => nullable ? $"bool.TryParse(args[index], out var v) ? v : null" : $"bool.Parse(args[index])",
            "System.Byte" => nullable ? $"byte.TryParse(args[index], out var v) ? v : null" : $"byte.Parse(args[index])",
            "System.SByte" => nullable ? $"sbyte.TryParse(args[index], out var v) ? v : null" : $"sbyte.Parse(args[index])",
            "System.Int16" => nullable ? $"short.TryParse(args[index], out var v) ? v : null" : $"short.Parse(args[index])",
            "System.UInt16" => nullable ? $"ushort.TryParse(args[index], out var v) ? v : null" : $"ushort.Parse(args[index])",
            "System.UInt32" => nullable ? $"uint.TryParse(args[index], out var v) ? v : null" : $"uint.Parse(args[index])",
            "System.UInt64" => nullable ? $"ulong.TryParse(args[index], out var v) ? v : null" : $"ulong.Parse(args[index])",
            "System.Char" => nullable ? $"char.TryParse(args[index], out var v) ? v : null" : $"char.Parse(args[index])",
            "System.DateTime" => nullable ? $"DateTime.TryParse(args[index], out var v) ? v : null" : $"DateTime.Parse(args[index])",
            "System.TimeSpan" => nullable ? $"TimeSpan.TryParse(args[index], out var v) ? v : null" : $"TimeSpan.Parse(args[index])",
            "System.Guid" => nullable ? $"Guid.TryParse(args[index], out var v) ? v : null" : $"Guid.Parse(args[index])",
            var other => $"Convert.ChangeType(args[index], typeof({(nullable ? $"{other}?" : other)}))"
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

    private static string GenerateHelpMessage(CommandModel commandModel, SubCommandModel? subCommand)
    {
        var commandName = commandModel.Name;
        var sb = new StringBuilder($"# {commandName}");

        if (!string.IsNullOrEmpty(commandModel.Summary))
        {
            sb.AppendLine($" - {commandModel.Summary}");
        }

        sb.AppendLine();

        if (!string.IsNullOrEmpty(commandModel.Description))
        {
            sb.AppendLine(commandModel.Description);
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
                { ShortName: { } shortName, LongName: { } longName } => $"-{shortName}, --{longName}",
                { ShortName: { } shortName } => $"-{shortName}",
                { LongName: { } longName } => $"--{longName}",
                _ => $"{option.Symbol.Name.ToSnakeCase()}"
            };

            table.Add([names, option.Description]);
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
                table.Add([command.Name, command.Summary]);

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
                             System.Console.ForegroundColor = System.ConsoleColor.Red;
                             System.Console.WriteLine($"{message}\n");
                             System.Console.ResetColor();
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
}