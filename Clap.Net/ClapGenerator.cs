using System.Collections.Immutable;
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

internal abstract record ArgumentModel(ISymbol Symbol);
internal record SubCommandArgumentModel(ISymbol Symbol) : ArgumentModel(Symbol);
internal record PositionalArgumentModel(ISymbol Symbol, AttributeData? Attribute, bool Required, int Index) : ArgumentModel(Symbol);
internal record ValueArgumentModel(ISymbol Symbol, AttributeData? Attribute, bool Required) : ArgumentModel(Symbol);


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

            var required = (member as IPropertySymbol)?.IsRequired ?? false;
            var attributes = member
                .GetAttributes()
                .Where(attr => attr.AttributeClass?.Name is not null)
                .ToDictionary(attr => attr.AttributeClass?.Name!);

            if (attributes.TryGetValue(nameof(CommandAttribute), out var command))
            {
                arguments.Add(new SubCommandArgumentModel(member));
                continue;
            }

            if (attributes.TryGetValue(nameof(ArgAttribute), out var argAttribute) is false)
            {
                arguments.Add(new PositionalArgumentModel(member, null, required, positionalIndex++));
                continue;
            }

            if (argAttribute.NamedArguments.FirstOrDefault(a => a.Key is "Index").Value.Value is int index)
            {
                arguments.Add(new PositionalArgumentModel(member, null, required, index));
                continue;
            }

            arguments.Add(new ValueArgumentModel(member, null, required));
        }

        return new CommandModel(
            symbol switch
            {
                INamedTypeSymbol { TypeKind: TypeKind.Class } => "class",
                INamedTypeSymbol { TypeKind: TypeKind.Struct } => "struct",
                _ => "class"
            },
            commandAttribute.NamedArguments.FirstOrDefault(a => a.Key is "Name").Value.Value?.ToString(),
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

        writer.WriteLine(
            $$"""
              public partial {{commandModel.Kind}} {{typeName}}
              {
                  public static {{fullName}} Parse(string[] args)
                  {
                      // {{commandModel.Arguments.Length}} arguments to parse
              """);

        writer.IncreaseIndent(by: 2);

        foreach (var argument in commandModel.Arguments)
        {
            switch (argument)
            {
                case PositionalArgumentModel positional:
                    writer.WriteLine($"// Argument '{positional.Symbol.Name}' is positional argument at index {positional.Index}");
                    writer.WriteLine($"{GetMemberTypeName(positional.Symbol)} {ToCamelCase(positional.Symbol.Name)} = default!;");
                    break;

                case ValueArgumentModel arg:
                    writer.WriteLine($"// Argument '{arg.Symbol.Name}' is named argument");
                    writer.WriteLine($"{GetMemberTypeName(arg.Symbol)} {ToCamelCase(arg.Symbol.Name)} = default!;");
                    break;
            }
        }

        writer.WriteLine();
        writer.WriteLine("""
                         var index = 0;
                         var positionalIndex = 0;
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
                when subCommandModels.FirstOrDefault(sc => GetMemberTypeName(c.Symbol) == sc?.Symbol.ToDisplayString(TypeNameFormat)) is { } subCommand:
                    {
                        writer.WriteLine($"// Setting subcommand '{subCommand.Symbol.ToDisplayString()}'");

                        foreach (var command in subCommand.Commands)
                        {
                            var name = command.Symbol.GetAttributes()
                                .First(attr => attr.AttributeClass?.Name is nameof(CommandAttribute))
                                .NamedArguments.FirstOrDefault(a => a.Key is "Name").Value.Value?.ToString()
                                ?? ToSnakeCase(command.Symbol.Name);

                            writer.WriteLine($"case \"{name}\":");
                        }

                        writer.IncreaseIndent();
                        writer.WriteLine($"return new {fullName}");
                        writer.WriteLine('{');
                        writer.IncreaseIndent();
                        writer.WriteLine($"{c.Symbol.Name} = {subCommand.Symbol.ToDisplayString(TypeNameFormat)}.Resolve(args[(index + 1)..])");

                        foreach (var arg in commandModel.Arguments.Where(a => a is not SubCommandArgumentModel))
                            writer.WriteLine($"{arg.Symbol.Name} = {ToCamelCase(arg.Symbol.Name)},");

                        writer.DecreaseIndent();
                        writer.WriteLine("};");
                        writer.DecreaseIndent();
                        writer.WriteLine();
                        break;
                    }

                case ValueArgumentModel arg:
                    {
                        var argAttribute = ParseArgAttributeFromSymbol(arg.Symbol.GetAttributes()
                            .First(attr => attr.AttributeClass?.Name is nameof(ArgAttribute))
                            .NamedArguments);

                        var name = argAttribute switch
                        {
                            { ShortName: var shortName and not '\0', LongName: { } longName } => $"\"-{shortName}\" or \"--{longName}\"",
                            { ShortName: var shortName and not '\0' } => $"\"-{shortName}\"",
                            { LongName: { } longName } => $"\"--{longName}\"",
                            _ => $"\"{ToSnakeCase(arg.Symbol.Name)}\""
                        };

                        writer.WriteLine($"// Setting attribute '{typeName}.{arg.Symbol.Name}'");
                        writer.WriteLine($"case {name}:");
                        writer.IncreaseIndent();
                        WriteArgValueConversion(arg.Symbol, writer);
                        writer.WriteLine("break;");
                        writer.DecreaseIndent();
                        writer.WriteLine();
                        break;
                    }

                case PositionalArgumentModel positional:
                    {
                        writer.WriteLine($"// Setting attribute '{typeName}.{positional.Symbol.Name}'");
                        writer.WriteLine($"case var arg when positionalIndex is {positional.Index}:");
                        writer.IncreaseIndent();
                        WriteArgValueConversion(positional.Symbol, writer);
                        writer.WriteLine("positionalIndex++;");
                        writer.WriteLine("break;");
                        writer.DecreaseIndent();
                        break;
                    }
            }
        }

        writer.WriteLine("""
                         case var arg when arg.StartsWith('-'):
                             Console.WriteLine($"Unknown parameter {arg}");
                             break;
                         """);

        writer.DecreaseIndent(by: 2);
        writer.WriteLine("""
                            }

                            index++;
                        }
                        """);

        writer.WriteLine();

        writer.WriteLine($"return new {fullName}");
        writer.WriteLine('{');
        writer.IncreaseIndent();

        foreach (var arg in commandModel.Arguments.Where(a => a is not SubCommandArgumentModel))
            writer.WriteLine($"{arg.Symbol.Name} = {ToCamelCase(arg.Symbol.Name)},");

        writer.DecreaseIndent(by: 3);
        writer.WriteLine("""
                                 };
                             }
                         }
                         """);

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
                  public static {{fullName}} Resolve(string[] args)
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
                .NamedArguments.FirstOrDefault(a => a.Key is "Name").Value.Value?.ToString()
                ?? ToSnakeCase(command.Symbol.Name);

            var commandFullName = command.Symbol.ToDisplayString(TypeNameFormat);
            writer.WriteLine($"\"{name}\" => ({fullName}){commandFullName}.Parse(args[1..]),");
        }

        writer.WriteLine("_ => throw new Exception(\"Unknown command\")");

        writer.DecreaseIndent(by: 3);
        writer.WriteLine("""
                                 };
                             }
                         }
                         """);

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteArgValueConversion(ISymbol member, Utf8IndentedWriter writer)
    {
        writer.WriteLine($"{ToCamelCase(member.Name)} = {GetArgConversion(member)};");
    }

    private static string? GetArgConversion(ISymbol member)
    {
        return GetMemberTypeName(member) switch
        {
            "System.String" or "System.String?" => "args[index]",
            "System.Int32" or "System.Int32?" => "int.Parse(args[index])",
            "System.Boolean" or "System.Boolean?" => "true",
            var type => $"throw new Exception(\"Type '{type}' is not supported\")"
        };
    }

    private static string GetMemberTypeName(ISymbol symbol)
    {
        return symbol switch
        {
            IPropertySymbol prop => prop.Type.ToDisplayString(TypeNameFormat),
            IFieldSymbol field => field.Type.ToDisplayString(TypeNameFormat),
            _ => throw new Exception($"Dont know how to get type for  {symbol.ToDisplayString()}")
        };
    }

    private static string ToCamelCase(string name)
    {
        return char.ToLower(name[0]) + name.Substring(1);
    }

    private static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder(char.ToLower(name[0]));

        foreach (var c in name.Substring(1))
        {
            if (char.IsUpper(c))
            {
                if (sb.Length > 0)
                    sb.Append('_');

                sb.Append(char.ToLower(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static ArgAttribute ParseArgAttributeFromSymbol(ImmutableArray<KeyValuePair<string, TypedConstant>> namedArguments)
    {
        var shortName = namedArguments.FirstOrDefault(a => a.Key is "ShortName").Value.Value;
        var longName = namedArguments.FirstOrDefault(a => a.Key is "LongName").Value.Value;
        var index = namedArguments.FirstOrDefault(a => a.Key is "Index").Value.Value;
        var description = namedArguments.FirstOrDefault(a => a.Key is "Description").Value.Value;
        var required = namedArguments.FirstOrDefault(a => a.Key is "Required").Value.Value;

        return new ArgAttribute
        {
            ShortName = shortName as char? ?? '\0',
            LongName = (string?)longName,
            Index = index as int? ?? -1,
            Description = (string?)description,
            Required = required as bool? ?? false
        };
    }
}