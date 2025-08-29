using System;
using System.CodeDom.Compiler;

namespace Clap.Net.Serialisation;

internal record BlockSyntaxBuilder : IDisposable
{
    private readonly IndentedTextWriter _writer;

    public BlockSyntaxBuilder(IndentedTextWriter writer)
    {
        _writer = writer;
        _writer.WriteLine("{");
        _writer.Indent++;
    }

    public void SingleLineComment(string comment) => _writer.WriteLine($"// {comment}");

    public void Assignment(string name, string value) => _writer.WriteLine($"{name} = {value};");

    public void Break() => _writer.WriteLine("break;");

    public BlockSyntaxBuilder If(string condition)
    {
        _writer.WriteLine($"if ({condition})");
        return new BlockSyntaxBuilder(_writer);
    }

    public BlockSyntaxBuilder WhileStatement(string expression)
    {
        _writer.WriteLine($"while ({expression})");
        return new BlockSyntaxBuilder(_writer);
    }

    public BlockSyntaxBuilder ForEachBlock(string label, string collection)
    {
        _writer.WriteLine($"foreach (var {label} in {collection})");
        return new BlockSyntaxBuilder(_writer);
    }

    public SwitchStatementSyntaxBuilder SwitchStatement(string expression)
    {
        return new SwitchStatementSyntaxBuilder(_writer, expression);
    }

    public ObjectInstanceSyntaxBuilder NewObject(string fullName)
    {
        return new ObjectInstanceSyntaxBuilder(_writer, fullName);
    }

    public void Dispose()
    {
        _writer.Indent--;
        _writer.WriteLine("}");
    }
}