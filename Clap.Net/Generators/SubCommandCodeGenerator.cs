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

        // Generate a local function per subcommand to handle help, version, and error propagation
        foreach (var command in commands)
        {
            var name = command.Symbol.GetAttributes()
                           .First(attr => attr.AttributeClass?.Name is nameof(CommandAttribute))
                           .NamedArguments.FirstOrDefault(a => a.Key is nameof(CommandAttribute.Name))
                           .Value.Value
                           ?.ToString()
                       ?? command.Symbol.Name.ToSnakeCase();

            var commandFullName = command.Symbol.ToDisplayString(CodeGeneratorConstants.FullNameDisplayFormat);

            writer.WriteMultiLine(
                $$"""
                  if (tokens[0] is Clap.Net.ValueLiteral("{{name}}"))
                  {
                      var __subResult = {{commandFullName}}.TryParse(tokens[1..]);
                      if (__subResult.IsError) return __subResult.Error;
                      if (__subResult.IsHelp) return __subResult.Help;
                      if (__subResult.IsVersion) return __subResult.Version;
                      return __subResult.Command!;
                  }
                  """);
            writer.WriteLine();
        }

        writer.WriteLine("throw new Exception(\"Unknown command\");");
    }
}