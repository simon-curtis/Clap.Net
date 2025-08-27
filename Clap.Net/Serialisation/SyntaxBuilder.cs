using System.CodeDom.Compiler;
using Microsoft.CodeAnalysis;

namespace Clap.Net.Serialisation;

internal record SyntaxBuilder(IndentedTextWriter Writer)
{
    public TypeSyntaxBuilder TypeDefinition(INamedTypeSymbol symbol) => new(Writer, symbol);

    public void BlockScopeNamespace(INamespaceSymbol symbolContainingNamespace)
    {
        var ns = symbolContainingNamespace.ToDisplayString();
        Writer.WriteLine($"namespace {ns};\n");
    }

    public void WriteNullable() => Writer.WriteLine("#nullable enable");

    public void WriteMultiLineComment(string comment)
    {
        Writer.WriteLine("/*");
        foreach (var line in comment.Split('\n'))
            Writer.WriteLine($"* {line.TrimEnd()}");
        Writer.WriteLine("*/");
    }
}