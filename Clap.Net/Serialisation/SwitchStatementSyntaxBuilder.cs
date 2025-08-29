using System;
using System.CodeDom.Compiler;

namespace Clap.Net.Serialisation;

internal record SwitchStatementSyntaxBuilder : IDisposable
{
    private readonly IndentedTextWriter _writer;

    public SwitchStatementSyntaxBuilder(IndentedTextWriter writer, string expression)
    {
        _writer = writer;
        _writer.WriteLine($"switch ({expression})");
        _writer.WriteLine("{");
        _writer.Indent++;
    }

    public BlockSyntaxBuilder Case(string expression)
    {
        _writer.WriteLine($"case {expression}:");
        return new BlockSyntaxBuilder(_writer);
    }

    public BlockSyntaxBuilder DefaultCase()
    {
        _writer.WriteLine("default:");
        return new BlockSyntaxBuilder(_writer);
    }

    public void Dispose()
    {
        _writer.Indent--;
        _writer.WriteLine("}");
    }
}