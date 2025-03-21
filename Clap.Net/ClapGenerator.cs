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
    string? Description,
    INamedTypeSymbol Symbol,
    ArgumentModel[] Arguments
);

internal record SubCommandModel(ISymbol Symbol, CommandModel[] Commands);

internal abstract record ArgumentModel(ISymbol Symbol, ITypeSymbol MemberType);
internal record SubCommandArgumentModel(ISymbol Symbol, ITypeSymbol MemberType) : ArgumentModel(Symbol, MemberType);
internal record PositionalArgumentModel(
    ISymbol Symbol,
    AttributeData? Attribute,
    ITypeSymbol MemberType,
    int Index,
    bool Required = false,
    bool Last = false
) : ArgumentModel(Symbol, MemberType);

internal record NamedArgumentModel(
    ISymbol Symbol,
    AttributeData? Attribute,
    string? Description,
    char? ShortName,
    string? LongName,
    ITypeSymbol MemberType,
    bool Required
) : ArgumentModel(Symbol, MemberType);

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
                arguments.Add(new SubCommandArgumentModel(member, memberType));
                continue;
            }

            if (attributes.TryGetValue(nameof(ArgAttribute), out var argAttribute) is false)
            {
                arguments.Add(new PositionalArgumentModel(member, null, memberType, positionalIndex++));
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
                arguments.Add(new PositionalArgumentModel(member, null, memberType, int.Parse(index.ToString())));
                continue;
            }

            if (argAttribute.NamedArguments.Any(a => a.Key is nameof(ArgAttribute.Last)))
            {
                arguments.Add(new PositionalArgumentModel(member, null, memberType, 0, true));
                continue;
            }

            arguments.Add(new PositionalArgumentModel(member, null, memberType, positionalIndex++));
        }

        return new CommandModel(
            symbol switch
            {
                INamedTypeSymbol { TypeKind: TypeKind.Class } @class => @class.IsRecord ? "record" : "class",
                INamedTypeSymbol { TypeKind: TypeKind.Struct } @struct => @struct.IsRecord ? "record struct" : "struct",
                _ => "class"
            },
            commandAttribute.NamedArguments.FirstOrDefault(a => a.Key is nameof(CommandAttribute.Name)).Value.Value?.ToString() ?? symbol.Name.ToSnakeCase(),
            commandAttribute.NamedArguments.FirstOrDefault(a => a.Key is "Description").Value.Value?.ToString(),
            namedTypeSymbol,
            [.. arguments]
        );
    }

    private static string GenerateCommandParseMethod(CommandModel commandModel, ImmutableArray<SubCommandModel?> subCommandModels)
    {
        var typeName = commandModel.Symbol.Name;
        var fullName = commandModel.Symbol.ToDisplayString(TypeNameFormat);

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

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(commandModel.Description))
        {
            sb.AppendLine(commandModel.Description);
            sb.AppendLine();
        }

        sb.Append("Usage: ");
        sb.Append(commandModel.Name ?? Assembly.GetExecutingAssembly().GetName().Name);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            sb.Append("[.exe]");

        sb.AppendLine(" [OPTIONS]");
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

        var subCommands = commandModel.Arguments
            .OfType<SubCommandArgumentModel>()
            .ToArray();

        if (subCommands.Length > 0)
        {
            // table.Clear();
            // foreach (var subCommandClass in subCommands)
            // {
            //     if (FindSubCommand(subCommandClass.Symbol, subCommandModels) is { } subCommand)
            //     {
            //         foreach (var command in subCommand.Commands)
            //             table.Add([command.Name, command.Description]);
            //     }
            // }

            // maxColumnLength = table.Max(o => o[0]?.Length ?? 0);

            // sb.AppendLine("Commands:");
            // foreach (var row in table)
            //     sb.AppendLine($"  {row[0]?.PadRight(maxColumnLength)}  {row[1]}");
            // sb.AppendLine();
        }

        writer.WriteLine($"private const string HelpMessage = \"\"\"");
        writer.IncreaseIndent();
        writer.WriteLine(sb.ToString());
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
                             System.Console.WriteLine(HelpMessage);
                             System.Environment.Exit(0);
                         }
                         """);
        writer.WriteLine();

        if (commandModel.Arguments.Length is 0)
        {
            writer.WriteLine($"return new {fullName}();");
            writer.DecreaseAndWriteLine("}");
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
                        writer.WriteLine($"{type} {NormaliseVariableName(arg.Symbol.Name.ToCamelCase())} = {defaultValue};");
                        break;
                    }

                case NamedArgumentModel arg:
                    {
                        var type = arg.MemberType.ToDisplayString(TypeNameFormat);
                        var defaultValue = GetDefaultValueString(arg.MemberType);

                        writer.WriteLine($"// Argument '{arg.Symbol.Name}' is a named argument");
                        writer.WriteLine($"{type} {NormaliseVariableName(arg.Symbol.Name.ToCamelCase())} = {defaultValue};");
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

        foreach (var argument in commandModel.Arguments)
        {
            switch (argument)
            {
                case SubCommandArgumentModel c
                when FindSubCommand(c.MemberType, subCommandModels) is { } subCommand:
                    {
                        writer.WriteLine($"// Setting subcommand '{subCommand.Symbol.ToDisplayString()}'");
                        writer.WriteLine("// Command switching swallows the rest of the arguments but previously");
                        writer.WriteLine("// parsed arguments still get applied to the model");

                        var names = new string[subCommand.Commands.Length];
                        for (var i = 0; i < subCommand.Commands.Length; i++)
                        {
                            var name = subCommand.Commands[i].Symbol.GetAttributes()
                                .First(attr => attr.AttributeClass?.Name is nameof(CommandAttribute))
                                .NamedArguments.FirstOrDefault(a => a.Key is nameof(CommandAttribute.Name)).Value.Value?.ToString()
                                ?? subCommand.Commands[i].Symbol.Name.ToSnakeCase();

                            names[i] = $"\"{name}\"";
                        }

                        writer.WriteLine($"case {string.Join(" or ", names)}:");
                        writer.IncreaseIndent();
                        writer.WriteLine($"return new {fullName}");
                        writer.WriteLine('{');
                        writer.IncreaseIndent();
                        writer.WriteLine($"{c.Symbol.Name} = {subCommand.Symbol.ToDisplayString(TypeNameFormat)}.Resolve(args[index..]),");

                        foreach (var arg in commandModel.Arguments.Where(a => a is not SubCommandArgumentModel))
                            writer.WriteLine($"{arg.Symbol.Name} = {NormaliseVariableName(arg.Symbol.Name.ToCamelCase())},");

                        writer.DecreaseIndent();
                        writer.WriteLine("};");
                        writer.DecreaseIndent();
                        writer.WriteLine();
                        break;
                    }

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
                        writer.IncreaseIndent();
                        writer.WriteLine(GetArgValueSetter(value.Symbol, value.MemberType));
                        writer.WriteLine("break;");
                        writer.DecreaseIndent();
                        writer.WriteLine();
                        break;
                    }

                case PositionalArgumentModel positional:
                    {
                        writer.WriteLine($"// Setting attribute '{fullName}.{positional.Symbol.Name}'");
                        writer.WriteLine($"case var arg when !arg.StartsWith('-') && positionalIndex is {(positional.Last ? (lastIndex + 1) : positional.Index)}:");
                        writer.IncreaseIndent();
                        writer.WriteLine(GetArgValueSetter(positional.Symbol, positional.MemberType));
                        writer.WriteLine("positionalIndex++;");
                        writer.WriteLine("break;");
                        writer.DecreaseIndent();
                        writer.WriteLine();
                        break;
                    }
            }
        }

        writer.WriteLine("""
                         case var arg when arg.StartsWith('-'):
                             System.Console.ForegroundColor = System.ConsoleColor.Red;
                             System.Console.WriteLine($"Unknown argument '{arg}'\n");
                             System.Console.ResetColor();
                             System.Console.WriteLine(HelpMessage);
                             System.Environment.Exit(0);
                             break;
                         """);


        writer.DecreaseAndWriteLine("}");
        writer.WriteLine();
        writer.WriteLine("index++;");

        writer.DecreaseAndWriteLine("}");

        writer.WriteLine();

        if (commandModel.Arguments.FirstOrDefault(a => a is SubCommandArgumentModel) is SubCommandArgumentModel reqCommand
            && reqCommand.MemberType.NullableAnnotation is not NullableAnnotation.Annotated)
        {
            var name = reqCommand.Symbol.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass?.Name is nameof(CommandAttribute))
                ?.NamedArguments.FirstOrDefault(a => a.Key is nameof(CommandAttribute.Name)).Value.Value?.ToString()
                ?? reqCommand.Symbol.Name.ToSnakeCase();

            writer.WriteLine($"""
                              System.Console.ForegroundColor = System.ConsoleColor.Red;
                              System.Console.WriteLine($"SubCommand '{name}' is required\n");
                              System.Console.ResetColor();
                              System.Console.WriteLine(HelpMessage);
                              System.Environment.Exit(0);
                              return default!;
                              """);
        }
        else
        {

            if (commandModel.Arguments
                .OfType<PositionalArgumentModel>()
                .Where(a => a.MemberType is IArrayTypeSymbol)
                .ToArray() is { Length: > 0 } requiredArrays)
            {
                foreach (var requiredArr in requiredArrays)
                {
                    var variableName = NormaliseVariableName(requiredArr.Symbol.Name.ToCamelCase());
                    writer.WriteLine($$"""
                                       if ({{variableName}}.Length is 0)
                                       {
                                           System.Console.ForegroundColor = System.ConsoleColor.Red;
                                           System.Console.WriteLine($"Positional array '{{variableName}}' is required\n");
                                           System.Console.ResetColor();
                                           System.Console.WriteLine(HelpMessage);
                                           System.Environment.Exit(0);
                                           return default!;
                                       }
                                       """);
                }

                writer.WriteLine();
            }

            writer.WriteLine($"return new {fullName}");
            writer.WriteLine('{');
            writer.IncreaseIndent();

            foreach (var arg in commandModel.Arguments.Where(a => a is not SubCommandArgumentModel))
            {
                var variableName = NormaliseVariableName(arg.Symbol.Name.ToCamelCase());
                writer.WriteLine($"{arg.Symbol.Name} = {variableName},");
            }

            writer.DecreaseIndent();
            writer.WriteLine("};");
        }

        writer.DecreaseAndWriteLine("}");
        writer.DecreaseAndWriteLine("}");

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

    private static string GetArgValueSetter(ISymbol member, ITypeSymbol memberType)
    {
        // If member is array
        if (memberType is IArrayTypeSymbol arrayType && arrayType.ElementType is not null)
        {
            var childType = arrayType.ElementType.ToDisplayString(TypeNameFormat);
            return $$"""
                     var builder = System.Collections.Immutable.ImmutableArray.CreateBuilder<{{childType}}>();
                     while (index < args.Length && !args[index].StartsWith('-')) 
                     {
                         builder.Add({{GetArgConversion(arrayType.ElementType)}});
                         index++;
                     }
                     {{NormaliseVariableName(member.Name.ToCamelCase())}} = builder.ToArray();
                     """;
        }

        return $"{NormaliseVariableName(member.Name.ToCamelCase())} = {GetArgConversion(memberType)};";
    }

    private static string? GetArgConversion(ISymbol member)
    {
        return member.ToDisplayString(TypeNameFormat) switch
        {
            "System.String" or "System.String?" => "args[index]",
            "System.Int32" or "System.Int32?" => "int.Parse(args[index])",
            "System.Boolean" or "System.Boolean?" => "true",
            var type => $"throw new Exception(\"Type '{type}' is not supported\")"
        };
    }

    private static string NormaliseVariableName(string name) => name switch
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

    private static SubCommandModel? FindSubCommand(ISymbol member, ImmutableArray<SubCommandModel?> subCommandModels)
    {
        var memberType = member.ToDisplayString(TypeNameFormat).TrimEnd('?');
        return subCommandModels.FirstOrDefault(sc => memberType == sc?.Symbol.ToDisplayString(TypeNameFormat).TrimEnd('?'));
    }
}