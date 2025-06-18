using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Text;
using Clap.Net.Extensions;
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

internal record SubCommandArgumentModel(
    ISymbol Symbol,
    ITypeSymbol MemberType,
    string Name,
    bool IsRequired);

internal abstract record ArgumentModel(
    ISymbol Symbol,
    ITypeSymbol MemberType,
    string VariableName,
    string? DefaultValue = null);

internal record PositionalArgumentModel(
    ISymbol Symbol,
    ITypeSymbol MemberType,
    string VariableName,
    string? DefaultValue,
    int Index = 0,
    bool Required = false
) : ArgumentModel(Symbol, MemberType, VariableName, DefaultValue);

internal record NamedArgumentModel(
    ISymbol Symbol,
    ITypeSymbol MemberType,
    string VariableName,
    string? DefaultValue,
    string? Help,
    char? Short,
    string? Long,
    string? Env,
    ArgAction? Action
) : ArgumentModel(Symbol, MemberType, VariableName, DefaultValue);

[Generator]
public class ClapGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat FullNameDisplayString =
        new(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
        );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilerInfoProvider = context
            .CompilationProvider
            .Select((c, _) => c.Assembly.Identity.Version.ToString());

        var commandCandidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, _) =>
                {
                    var decl = (TypeDeclarationSyntax)ctx.Node;
                    var symbol = (INamedTypeSymbol)ctx.SemanticModel.GetDeclaredSymbol(decl)!;
                    return GetCommandModel(symbol, decl);
                }
            )
            .Where(symbol => symbol != null);

        var subCommandCandidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, _) =>
                {
                    var decl = (TypeDeclarationSyntax)ctx.Node;
                    var symbol = (INamedTypeSymbol)ctx.SemanticModel.GetDeclaredSymbol(decl)!;

                    var subCommandAttribute = symbol
                        .GetAttributes()
                        .FirstOrDefault(attr => attr.AttributeClass?.Name is nameof(SubCommandAttribute));

                    if (subCommandAttribute is null)
                        return null;

                    var commands = new List<CommandModel>();
                    foreach (var nestedTypeSymbol in symbol.GetTypeMembers())
                    {
                        if (nestedTypeSymbol.DeclaredAccessibility != Accessibility.Public)
                            continue;

                        // get the syntax of the nested symbol
                        var typeDeclarationSyntax = nestedTypeSymbol.DeclaringSyntaxReferences
                            .OfType<TypeDeclarationSyntax>()
                            .FirstOrDefault();

                        if (GetCommandModel(nestedTypeSymbol, typeDeclarationSyntax) is { } model)
                            commands.Add(model);
                    }

                    return new SubCommandModel(symbol, [.. commands]);
                }
            )
            .Where(symbol => symbol != null);

        var combined = commandCandidates.Collect()
            .Combine(subCommandCandidates.Collect())
            .Combine(compilerInfoProvider);

        context.RegisterSourceOutput(combined, (spc, tuple) =>
        {
            var ((commandModels, subCommandModels), version) = tuple;

            foreach (var commandModel in commandModels)
            {
                if (commandModel is null)
                    continue;

                var output = GenerateCommandParseMethod(commandModel, subCommandModels, version);
                spc.AddSource($"{commandModel.Symbol.Name}.ParseMethod.g.cs", SourceText.From(output, Encoding.UTF8));
            }

            foreach (var subCommand in subCommandModels)
            {
                if (subCommand is null)
                    continue;

                var output = GenerateSubCommandParseMethod(subCommand);
                spc.AddSource($"{subCommand.Symbol.Name}.ParseMethod.g.cs", SourceText.From(output, Encoding.UTF8));
            }
        });
    }

    private static CommandModel? GetCommandModel(
        INamedTypeSymbol commandCandidateSymbol,
        TypeDeclarationSyntax? typeDeclarationSyntax)
    {
        var commandAttribute = commandCandidateSymbol
            .GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name is nameof(CommandAttribute));

        if (commandAttribute is null)
            return null;

        var arguments = new List<ArgumentModel>();
        SubCommandArgumentModel? subCommandArgumentModel = null;

        var positionalIndex = 0;
        foreach (var member in commandCandidateSymbol.GetMembers())
        {
            if (member is not IPropertySymbol and not IFieldSymbol
                || member.DeclaredAccessibility is not Accessibility.Public)
                continue;

            var memberType = member switch
            {
                IPropertySymbol property => property.Type,
                IFieldSymbol field => field.Type,
                _ => null,
            };

            if (memberType is null)
                continue;

            var defaultValue = member switch
            {
                IParameterSymbol prop => prop.HasExplicitDefaultValue ? prop.ExplicitDefaultValue?.ToString() : null,
                IFieldSymbol field => field.HasConstantValue ? field.ConstantValue?.ToString() : null,
                _ => GetDefaultValueString(memberType)
            };

            var attributes = member
                .GetAttributes()
                .Where(attr => attr.AttributeClass?.Name is not null)
                .ToDictionary(attr => attr.AttributeClass?.Name!);

            if (attributes.TryGetValue(nameof(CommandAttribute), out var command))
            {
                var name = command?.NamedArguments
                    .FirstOrDefault(a => a.Key is nameof(CommandAttribute.Name))
                    .Value.Value?.ToString() ?? member.Name;

                var isRequired = member switch
                {
                    IPropertySymbol property => property.IsRequired,
                    IFieldSymbol field => field.IsRequired,
                    _ => false
                };

                subCommandArgumentModel = new SubCommandArgumentModel(member, memberType, name, isRequired);
                continue;
            }

            var variableName = GetVariableName(member.Name.ToCamelCase());

            if (!attributes.TryGetValue(nameof(ArgAttribute), out var argAttribute))
            {
                arguments.Add(new PositionalArgumentModel(
                    member,
                    memberType,
                    variableName,
                    defaultValue,
                    positionalIndex++)
                );
                continue;
            }

            var argNamedArguments = argAttribute.NamedArguments.ToDictionary(a => a.Key, a => a.Value.Value);
            var @short = argNamedArguments.GetOrDefault(nameof(ArgAttribute.Short)) as char?;
            var @long = argNamedArguments.GetOrDefault(nameof(ArgAttribute.Long)) as string;
            var argAction = argNamedArguments.GetOrDefault(nameof(ArgAttribute.Action)) as ArgAction?;

            ArgumentModel argument = @short is not null || @long is not null
                ? new NamedArgumentModel(
                    member,
                    memberType,
                    variableName,
                    defaultValue,
                    Help: argNamedArguments.GetOrDefault(nameof(ArgAttribute.Help)) as string,
                    Short: @short,
                    Long: @long,
                    Env: argNamedArguments.GetOrDefault(nameof(ArgAttribute.Env)) as string,
                    argAction
                )
                : new PositionalArgumentModel(member, memberType, variableName, defaultValue, positionalIndex++);

            arguments.Add(argument);
        }

        var args = commandAttribute.NamedArguments
            .ToDictionary(a => a.Key, a => a.Value.Value);

        var commentary = typeDeclarationSyntax is not null
            ? ExtractSummary(typeDeclarationSyntax.ToFullString().AsSpan())
            : null;

        return new CommandModel(
            Kind: commandCandidateSymbol switch
            {
                { TypeKind: TypeKind.Class } => commandCandidateSymbol.IsRecord ? "record" : "class",
                { TypeKind: TypeKind.Struct } => commandCandidateSymbol.IsRecord ? "record struct" : "struct",
                _ => "class"
            },
            Name: args.GetOrDefault(nameof(CommandAttribute.Name)) as string ??
                  commandCandidateSymbol.Name.ToSnakeCase(),
            About: args.GetOrDefault(nameof(CommandAttribute.About)) as string ?? commentary?.About,
            LongAbout: args.GetOrDefault(nameof(CommandAttribute.LongAbout)) as string ?? commentary?.LongAbout,
            Version: args.GetOrDefault(nameof(CommandAttribute.Version)) as string,
            Symbol: commandCandidateSymbol,
            SubCommandArgumentModel: subCommandArgumentModel,
            Arguments: [.. arguments]
        );
    }

    private static string GenerateCommandParseMethod(
        CommandModel commandModel,
        ImmutableArray<SubCommandModel?> subCommandModels,
        string? assemblyVersion)
    {
        try
        {
            var typeName = commandModel.Symbol.Name;
            var fullName = commandModel.Symbol.ToDisplayString(FullNameDisplayString);
            var subCommand = commandModel.SubCommandArgumentModel is { } c
                ? FindSubCommand(c.MemberType, subCommandModels)
                : null;

            var version = commandModel.Version ?? assemblyVersion;

            var ns = commandModel.Symbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : commandModel.Symbol.ContainingNamespace.ToDisplayString();


            using var textWriter = new StringWriter();
            var writer = new IndentedTextWriter(textWriter, "    ");

            writer.WriteLine("#nullable enable");
            writer.WriteLine();

            writer.WriteLine(GetHeader(commandModel));

            if (ns is not null)
            {
                writer.WriteLine($"namespace {ns};\n");
                writer.WriteLine();
            }

            if (commandModel.Symbol.ContainingType is not null)
            {
                writer.WriteLine($"public partial class {commandModel.Symbol.ContainingType.Name}");
                writer.WriteLine("{");
                writer.Indent++;
            }

            writer.WriteMultiLine($$"""
                                    public partial {{commandModel.Kind}} {{typeName}}
                                    {
                                    """);

            writer.Indent++;

            writer.WriteLine("private const string HelpMessage = ");
            writer.Indent++;
            writer.WriteLine("\"\"\"");
            writer.WriteMultiLine(GenerateHelpMessage(commandModel, subCommand));
            writer.WriteLine("\"\"\";");
            writer.Indent--;
            writer.WriteLine();

            writer.WriteMultiLine($$"""
                                    public static {{fullName}} Parse(System.ReadOnlySpan<string> args) 
                                    {
                                        switch (TryParse(args))
                                        { 
                                            case { IsT0: true, AsT0: var result }:
                                                return result;

                                            case { IsT2: true, AsT2: { Version: var version } }:
                                                System.Console.WriteLine(version);
                                                System.Environment.Exit(0);
                                                break;

                                            case { IsT3: true, AsT3: { Message: var message } }:
                                                DisplayError(message);
                                                System.Environment.Exit(0);
                                                break;

                                            // Includes ShowHelp
                                            default:
                                                PrintHelpMessage();
                                                System.Environment.Exit(0);
                                                break;
                                        }

                                        return default!; // Just to shut the compiler up
                                    }

                                    public static Clap.Net.Models.ParseResult<{{fullName}}> TryParse(System.ReadOnlySpan<string> args)
                                    {
                                    """);

            writer.Indent++;

            writer.WriteMultiLine("""
                                  if (args.Length > 0 && args[0] is "-h" or "--help") 
                                  {
                                      return new Clap.Net.Models.ShowHelp();
                                  }
                                  """);
            writer.WriteLine();

            if (version is not null)
            {
                writer.WriteMultiLine($$"""
                                        if (args.Length > 0 && args[0] is "-v" or "--version")
                                        {
                                            return new Clap.Net.Models.ShowVersion("{{version}}");
                                        }
                                        """);
                writer.WriteLine();
            }

            if (commandModel.Arguments.Length is 0 && commandModel.SubCommandArgumentModel is null)
            {
                writer.WriteLine($"return new {fullName}();");
                writer.Indent--;
                writer.WriteLine("}");
                WriteHelperMethods(writer);
                writer.Indent--;
                writer.WriteLine("}");

                if (commandModel.Symbol.ContainingType is not null)
                {
                    writer.Indent--;
                    writer.WriteLine("}");
                }

                writer.Flush();
                return textWriter.ToString();
            }

            foreach (var arg in commandModel.Arguments.OfType<NamedArgumentModel>())
            {
                var initialValue = arg.DefaultValue ?? "default";
                var defaultValue = arg.Env is { } env
                    ? $"Environment.GetEnvironmentVariable(\"{env}\") is {{ }} env ? {GetArgConversion(arg.MemberType, "env")} : {initialValue}"
                    : initialValue;

                writer.WriteLine($"// Argument '{arg.Symbol.Name}' is a named argument");

                if (arg.MemberType is INamedTypeSymbol { Name: "IEnumerable" } namedTypeSymbol)
                {
                    var elementType = namedTypeSymbol.TypeArguments.First().ToDisplayString(FullNameDisplayString);
                    writer.WriteLine($"System.Collections.Generic.List<{elementType}> {arg.VariableName} = [];");
                }
                else
                {
                    var type = arg.MemberType.ToDisplayString(FullNameDisplayString);
                    writer.WriteLine($"{type} {arg.VariableName} = {defaultValue};");
                }

            }

            foreach (var arg in commandModel.Arguments.OfType<PositionalArgumentModel>())
            {
                var type = arg.MemberType.ToDisplayString(FullNameDisplayString);
                writer.WriteLine($"// Argument '{arg.Symbol.Name}' is positional argument at index {arg.Index}");
                writer.WriteLine($"{type} {arg.VariableName} = {arg.DefaultValue ?? "default"};");
            }

            writer.WriteLine();

            var positionalArgs = commandModel.Arguments.OfType<PositionalArgumentModel>().ToArray();
            if (positionalArgs.Length > 0) writer.WriteLine("var positionalIndex = 0;");

            writer.WriteMultiLine("""
                                  var index = 0;
                                  while (index < args.Length)
                                  {
                                      switch (args[index])
                                      {
                                  """);

            writer.Indent += 2;

            if (subCommand is not null)
            {
                writer.WriteLine($"// Setting subcommand '{subCommand.Symbol.ToDisplayString()}'");
                writer.WriteLine("// Command switching swallows the rest of the arguments but previously");
                writer.WriteLine("// parsed arguments still get applied to the model");

                var names = new string[subCommand.Commands.Length];
                for (var i = 0; i < subCommand.Commands.Length; i++)
                    names[i] = $"\"{subCommand.Commands[i].Name}\"";

                writer.WriteLine($"case {string.Join(" or ", names)}:");
                writer.Indent++;
                writer.WriteMultiLine($"""
                                       var subCommand = {subCommand.Symbol.ToDisplayString(FullNameDisplayString)}.Resolve(args[index..]);
                                       if (subCommand.IsT1) return subCommand.AsT1;
                                       if (subCommand.IsT2) return subCommand.AsT2;
                                       if (subCommand.IsT3) return subCommand.AsT3;
                                       """);

                writer.WriteLine($"return new {fullName}");
                writer.WriteLine('{');
                writer.Indent++;
                writer.WriteLine($"{commandModel.SubCommandArgumentModel!.Symbol.Name} = subCommand.AsT0,");

                foreach (var arg in commandModel.Arguments)
                    writer.WriteLine($"{arg.Symbol.Name} = {arg.VariableName},");

                writer.Indent--;
                writer.WriteLine("};");
                writer.Indent--;
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
                        writer.Indent++;
                        writer.WriteLine("index++;");
                        SetNamedArgumentValue(writer, value);
                        writer.WriteLine("break;");
                        writer.Indent--;
                        writer.WriteLine("}");
                        writer.WriteLine();
                        break;
                    }

                    case PositionalArgumentModel positional:
                    {
                        writer.WriteLine($"// Setting attribute '{fullName}.{positional.Symbol.Name}'");
                        writer.WriteLine(
                            $"case var arg when !arg.StartsWith('-') && positionalIndex is {positional.Index}:");
                        writer.WriteLine("{");
                        writer.Indent++;
                        SetPositionalValue(writer, positional);
                        writer.WriteLine("positionalIndex++;");
                        writer.WriteLine("break;");
                        writer.Indent--;
                        writer.WriteLine("}");
                        writer.WriteLine();
                        break;
                    }
                }
            }

            writer.WriteMultiLine("""
                                  case var arg:
                                      return new Clap.Net.Models.ParseError($"Unknown argument '{arg}'");
                                  """);


            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine();
            writer.Indent--;
            writer.WriteLine("}");

            writer.WriteLine();

            if (commandModel.SubCommandArgumentModel is { } reqCommand
                && (reqCommand.IsRequired
                    || reqCommand.MemberType.NullableAnnotation is not NullableAnnotation.Annotated))
            {
                writer.WriteLine(
                    $"return new Clap.Net.Models.ParseError(\"SubCommand '{reqCommand.Name}' is required\");");
            }
            else
            {
                writer.WriteLine();
                var requiredArguments = commandModel.Arguments
                    .OfType<PositionalArgumentModel>()
                    .Where(a => a.Required
                                || a.MemberType.NullableAnnotation is not NullableAnnotation.Annotated
                                || a.DefaultValue is not null)
                    .ToArray();

                if (requiredArguments.Length > 0)
                {
                    writer.WriteMultiLine($$"""
                                            if ({{string.Join(" || ", requiredArguments.Select(a => $"{a.VariableName} == default"))}})
                                            {
                                                var sb = new System.Text.StringBuilder();
                                            """);
                    writer.Indent++;

                    foreach (var requiredArg in requiredArguments)
                    {
                        switch (requiredArg.Symbol)
                        {
                            case IArrayTypeSymbol:
                            {
                                writer.WriteMultiLine($"""
                                                       if ({requiredArg.VariableName}.Length is 0)
                                                           sb.AppendLine("  <{requiredArg.Symbol.Name}>");
                                                       """);
                                break;
                            }

                            default:
                            {
                                writer.WriteMultiLine($"""
                                                       if ({requiredArg.VariableName} == default)
                                                           sb.AppendLine("  <{requiredArg.Symbol.Name}>");
                                                       """);
                                break;
                            }
                        }
                    }

                    writer.WriteLine(
                        @"return new Clap.Net.Models.ParseError($""The following required arguments were not provided:\n{sb.ToString()}"");");
                    writer.Indent--;
                    writer.WriteLine("}");
                }

                writer.WriteLine();
                writer.WriteLine($"return new {fullName}");
                writer.WriteLine('{');
                writer.Indent++;

                foreach (var arg in commandModel.Arguments)
                {
                    var variableName = GetVariableName(arg.Symbol.Name.ToCamelCase());
                    writer.WriteLine($"{arg.Symbol.Name} = {variableName},");
                }

                writer.Indent--;
                writer.WriteLine("};");
            }

            writer.Indent--;
            writer.WriteLine("}"); // This closes the Parse method
            WriteHelperMethods(writer);
            writer.Indent--;
            writer.WriteLine("}"); // This closes the Class/Struct body

            if (commandModel.Symbol.ContainingType is not null)
            {
                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Flush();
            return textWriter.ToString();
        }
        catch (Exception ex)
        {
            throw new Exception($"It happened while generating the source code: {ex.StackTrace}", ex);
        }
    }

    private static string GenerateSubCommandParseMethod(SubCommandModel subCommandModel)
    {
        var (symbol, commands) = subCommandModel;
        var typeName = symbol.Name;
        var fullName = symbol.ToDisplayString(FullNameDisplayString);
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
                  public static Clap.Net.Models.ParseResult<{{fullName}}> Resolve(System.ReadOnlySpan<string> args)
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
                           .NamedArguments.FirstOrDefault(a => a.Key is nameof(CommandAttribute.Name)).Value.Value
                           ?.ToString()
                       ?? command.Symbol.Name.ToSnakeCase();

            var commandFullName = command.Symbol.ToDisplayString(FullNameDisplayString);
            writer.WriteLine($"\"{name}\" => {commandFullName}.TryParse(args[1..]).ChangeType<{fullName}>(),");
        }

        writer.WriteLine("_ => throw new Exception(\"Unknown command\")");

        writer.DecreaseAndWriteLine("};");
        writer.DecreaseAndWriteLine("}");
        writer.DecreaseAndWriteLine("}");

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void SetPositionalValue(IndentedTextWriter writer, PositionalArgumentModel argument)
    {
        switch (argument.MemberType)
        {
            case IArrayTypeSymbol { ElementType: { } elementType }:
            {
                WriteArraySetter(writer, argument.VariableName, elementType);
                break;
            }

            case INamedTypeSymbol { Name: "IEnumerable" } namedTypeSymbol:
            {
                var elementType = namedTypeSymbol.TypeArguments.First();
                writer.WriteMultiLine($"""
                                       {argument.VariableName}.Add({GetArgConversion(elementType)});
                                       index++;
                                       """);
                break;
            }

            default:
                writer.WriteMultiLine($"""
                                       {argument.VariableName} = {GetArgConversion(argument.MemberType)};
                                       index++;
                                       """);
                break;
        }
    }

    private static void SetNamedArgumentValue(IndentedTextWriter writer, NamedArgumentModel argument)
    {
        if (argument.MemberType.Name is "Boolean")
        {
            writer.WriteMultiLine($$"""
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

        switch (argument.MemberType)
        {
            case IArrayTypeSymbol { ElementType: { } elementType }:
            {
                WriteArraySetter(writer, argument.VariableName, elementType);
                break;
            }

            case INamedTypeSymbol { Name: "IEnumerable" } namedTypeSymbol:
            {
                var elementType = namedTypeSymbol.TypeArguments.First();
                writer.WriteMultiLine($"""
                                       {argument.VariableName}.Add({GetArgConversion(elementType)});
                                       index++;
                                       """);
                break;
            }

            default:
                writer.WriteMultiLine($"""
                                       {argument.VariableName} = {GetArgConversion(argument.MemberType)};
                                       index++;
                                       """);
                break;
        }
    }

    private static void WriteArraySetter(IndentedTextWriter writer, string variableName, ITypeSymbol elementType)
    {
        var childType = elementType.ToDisplayString(FullNameDisplayString);
        writer.WriteMultiLine($$"""
                                var builder = System.Collections.Immutable.ImmutableArray.CreateBuilder<{{childType}}>();
                                while (index < args.Length && !args[index].StartsWith('-')) 
                                {
                                    builder.Add({{GetArgConversion(elementType)}});
                                    index++;
                                }
                                {{variableName}} = builder.ToArray();
                                """);
    }

    private static string GetArgConversion(ITypeSymbol member, string variableName = "args[index]")
    {
        var nullable = member.NullableAnnotation is NullableAnnotation.Annotated;
        var fullName = member.ToDisplayString(FullNameDisplayString);
        return member.Name switch
        {
            "String" => variableName,
            "Int32" => nullable
                ? $"int.TryParse({variableName}, out var v) ? v : null"
                : $"int.Parse({variableName})",
            "Int64" => nullable
                ? $"long.TryParse({variableName}, out var v) ? v : null"
                : $"long.Parse({variableName})",
            "Single" => nullable
                ? $"float.TryParse({variableName}, out var v) ? v : null"
                : $"float.Parse({variableName})",
            "Double" => nullable
                ? $"double.TryParse({variableName}, out var v) ? v : null"
                : $"double.Parse({variableName})",
            "Decimal" => nullable
                ? $"decimal.TryParse({variableName}, out var v) ? v : null"
                : $"decimal.Parse({variableName})",
            "Boolean" => nullable
                ? $"bool.TryParse({variableName}, out var v) ? v : null"
                : $"bool.Parse({variableName})",
            "Byte" => nullable
                ? $"byte.TryParse({variableName}, out var v) ? v : null"
                : $"byte.Parse({variableName})",
            "SByte" => nullable
                ? $"sbyte.TryParse({variableName}, out var v) ? v : null"
                : $"sbyte.Parse({variableName})",
            "Int16" => nullable
                ? $"short.TryParse({variableName}, out var v) ? v : null"
                : $"short.Parse({variableName})",
            "UInt16" => nullable
                ? $"ushort.TryParse({variableName}, out var v) ? v : null"
                : $"ushort.Parse({variableName})",
            "UInt32" => nullable
                ? $"uint.TryParse({variableName}, out var v) ? v : null"
                : $"uint.Parse({variableName})",
            "UInt64" => nullable
                ? $"ulong.TryParse({variableName}, out var v) ? v : null"
                : $"ulong.Parse({variableName})",
            "Char" => nullable
                ? $"char.TryParse({variableName}, out var v) ? v : null"
                : $"char.Parse({variableName})",
            "DateTime" => nullable
                ? $"DateTime.TryParse({variableName}, out var v) ? v : null"
                : $"DateTime.Parse({variableName})",
            "TimeSpan" => nullable
                ? $"TimeSpan.TryParse({variableName}, out var v) ? v : null"
                : $"TimeSpan.Parse({variableName})",
            "Guid" => nullable
                ? $"Guid.TryParse({variableName}, out var v) ? v : null"
                : $"Guid.Parse({variableName})",
            _ => $"Convert.ChangeType({variableName}, typeof({(nullable ? $"{fullName}?" : fullName)}))"
        };
    }

    private static string? GetTypeParameter(INamedTypeSymbol namedTypeSymbol)
    {
        return namedTypeSymbol.TypeArguments.FirstOrDefault()?.ToDisplayString(FullNameDisplayString);
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
        IArrayTypeSymbol => "[]",
        _ => "default!"
    };

    private static SubCommandModel? FindSubCommand(
        ITypeSymbol member,
        ImmutableArray<SubCommandModel?> subCommandModels)
    {
        var memberType = member.ToDisplayString().TrimEnd('?');
        return subCommandModels.FirstOrDefault(sc =>
            memberType == sc?.Symbol.ToDisplayString(FullNameDisplayString).TrimEnd('?'));
    }

    private static string GenerateHelpMessage(CommandModel commandModel, SubCommandModel? subCommand)
    {
        var sb = new StringBuilder();

        var about = commandModel.LongAbout ?? commandModel.About;
        if (!string.IsNullOrEmpty(about))
        {
            sb.AppendLine(about!.Trim());
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

    private static void WriteHelperMethods(IndentedTextWriter writer)
    {
        writer.WriteLine();
        writer.WriteMultiLine("""
                              public static void DisplayError(string message)
                              {
                                  var previousColour = System.Console.ForegroundColor;
                                  System.Console.ForegroundColor = System.ConsoleColor.Red;
                                  System.Console.WriteLine($"{message}\n");
                                  System.Console.ForegroundColor = previousColour;
                                  PrintHelpMessage();
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

    private static readonly char[] AllowedCharacters = [' ', '\t', '\n', '\r', '&'];

    private static (string About, string? LongAbout)? ExtractSummary(ReadOnlySpan<char> source)
    {
        const string summaryStartTag = "<summary>";
        const string summaryEndTag = "</summary>";

        var start = source.IndexOf(summaryStartTag.AsSpan());
        var end = source.IndexOf(summaryEndTag.AsSpan());

        if (start == -1 || end == -1 || end < start)
            return null;

        // Slice between <summary> and </summary>
        start += summaryStartTag.Length;
        var contentSpan = source.Slice(start, end - start);

        var lines = new List<string>();
        while (!contentSpan.IsEmpty)
        {
            var newLineIndex = contentSpan.IndexOf('\n');
            ReadOnlySpan<char> line;

            if (newLineIndex >= 0)
            {
                line = contentSpan.Slice(0, newLineIndex);
                contentSpan = contentSpan.Slice(newLineIndex + 1);
            }
            else
            {
                line = contentSpan;
                contentSpan = ReadOnlySpan<char>.Empty;
            }

            if (line.StartsWith("///".AsSpan()))
                line = line.Slice(3);

            var trimmed = RemoveInvalidCharacters(line).Trim();
            if (trimmed.Length > 0)
                lines.Add(trimmed);
        }

        return (
            About: lines[0],
            LongAbout: lines.Count is 1 ? null : string.Join("\n", lines)
        );
    }

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

    private static string GetHeader(CommandModel commandModel)
    {
        return $"""
                /*
                * Name: {commandModel.Name}
                * About: {commandModel.About}
                * Long About: {commandModel.LongAbout}
                */
                """;
    }
}