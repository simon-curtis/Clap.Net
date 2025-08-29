using System.CodeDom.Compiler;
using System.Linq;
using System.Reflection;
using Clap.Net.Extensions;
using Clap.Net.Models;
using Clap.Net.Serialisation;
using Microsoft.CodeAnalysis;

namespace Clap.Net.Generators;

internal static class SubCommandCodeGenerator
{
    private static readonly SymbolDisplayFormat FullNameDisplayString =
        new(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

    public static void GenerateSourceCode(
        IndentedTextWriter writer,
        SyntaxBuilder syntaxBuilder,
        SubCommandModel commandModel)
    {
        var (symbol, commands) = commandModel;
        var typeName = symbol.Name;
        var fullName = symbol.ToDisplayString(FullNameDisplayString);

        writer.WriteLine("#nullable enable");
        writer.WriteLine();

        if (!commandModel.Symbol.ContainingNamespace.IsGlobalNamespace)
            syntaxBuilder.BlockScopeNamespace(commandModel.Symbol.ContainingNamespace);

        using var containerType = commandModel.Symbol.ContainingType is { } containingType
            ? syntaxBuilder.TypeDefinition(containingType)
            : null;

        using var typeBuilder = syntaxBuilder.TypeDefinition(commandModel.Symbol);
        using var method = typeBuilder.Method(
            BindingFlags.Public | BindingFlags.Static,
            $"Clap.Net.Models.ParseResult<{fullName}>",
            "Resolve",
            ["System.ReadOnlySpan<Clap.Net.IToken> tokens"]);

        writer.WriteMultiLine(
            $$"""
              // {{commands.Count}} commands to parse
              return tokens[0] switch 
              {
              """);

        writer.Indent += 1;

        foreach (var command in commands)
        {
            var name = command.Symbol.GetAttributes()
                           .First(attr => attr.AttributeClass?.Name is nameof(CommandAttribute))
                           .NamedArguments.FirstOrDefault(a => a.Key is nameof(CommandAttribute.Name))
                           .Value.Value
                           ?.ToString()
                       ?? command.Symbol.Name.ToSnakeCase();

            var commandFullName = command.Symbol.ToDisplayString(FullNameDisplayString);
            writer.WriteLine(
                $"Clap.Net.ValueLiteral(\"{name}\") => {commandFullName}.TryParse(tokens[1..]).ChangeType<{fullName}>(),");
        }

        writer.WriteLine("_ => throw new Exception(\"Unknown command\")");

        writer.Indent--;
        writer.WriteLine("};");
    }
}