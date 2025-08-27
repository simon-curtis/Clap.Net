using System.CodeDom.Compiler;
using Clap.Net.Extensions;
using Clap.Net.Models;
using Microsoft.CodeAnalysis;

namespace Clap.Net.Generators;

internal static class SubCommandCodeGenerator
{
    private static readonly SymbolDisplayFormat FullNameDisplayString =
        new(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);

    public static void GenerateSourceCode(IndentedTextWriter writer, SubCommandModel subCommandModel)
    {
        var (symbol, commands) = subCommandModel;
        var typeName = symbol.Name;
        var fullName = symbol.ToDisplayString(FullNameDisplayString);
        var ns = symbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : symbol.ContainingNamespace.ToDisplayString();

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
                  public static Clap.Net.Models.ParseResult<{{fullName}}> Resolve(System.ReadOnlySpan<Clap.Net.IToken> tokens)
                  {
                      // {{commands.Count}} commands to parse
                      return tokens[0] switch 
                      {
              """);

        writer.Indent += 3;

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

        writer.Indent--;
        writer.WriteLine("}");

        writer.Indent--;
        writer.WriteLine("}");
    }
}