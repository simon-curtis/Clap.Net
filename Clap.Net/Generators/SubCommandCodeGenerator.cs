using System.CodeDom.Compiler;
using System.Linq;
using System.Reflection;
using Clap.Net.Extensions;
using Clap.Net.Models;
using Clap.Net.Serialisation;

namespace Clap.Net.Generators;

internal static class SubCommandCodeGenerator
{
    public static void GenerateSourceCode(
        IndentedTextWriter writer,
        SyntaxBuilder syntaxBuilder,
        SubCommandModel commandModel)
    {
        var (symbol, commands) = commandModel;
        var fullName = symbol.ToDisplayString(CodeGeneratorConstants.FullNameDisplayFormat);

        writer.WriteLine("#nullable enable");
        writer.WriteLine();

        if (!commandModel.Symbol.ContainingNamespace.IsGlobalNamespace)
            syntaxBuilder.BlockScopeNamespace(commandModel.Symbol.ContainingNamespace);

        using var containerType = commandModel.Symbol.ContainingType is { } containingType
            ? syntaxBuilder.TypeDefinition(containingType)
            : null;

        using var typeBuilder = syntaxBuilder.TypeDefinition(commandModel.Symbol);

        var resultClassName = ParseResultGenerator.CreateResultClassName(fullName);
        ParseResultGenerator.GenerateParseResultClass(writer, fullName, resultClassName);
        writer.WriteLine();

        using var method = typeBuilder.Method(
            BindingFlags.Public | BindingFlags.Static,
            resultClassName,
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

            var commandFullName = command.Symbol.ToDisplayString(CodeGeneratorConstants.FullNameDisplayFormat);
            writer.WriteLine(
                $"Clap.Net.ValueLiteral(\"{name}\") => {commandFullName}.TryParse(tokens[1..]).ChangeType<{fullName}>(),");
        }

        writer.WriteLine("_ => throw new Exception(\"Unknown command\")");

        writer.Indent--;
        writer.WriteLine("};");
    }
}