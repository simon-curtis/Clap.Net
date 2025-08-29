using System;
using System.CodeDom.Compiler;

namespace Clap.Net.Serialisation;

internal record ObjectInstanceSyntaxBuilder : IDisposable
{
    private readonly IndentedTextWriter _writer;

    public ObjectInstanceSyntaxBuilder(IndentedTextWriter writer, string fullName)
    {
        _writer = writer;
        _writer.WriteLine($"new {fullName}");
        _writer.WriteLine("{");
        _writer.Indent++;
    }

    public void WriteField(string name, string value)
    {
        _writer.WriteLine($"{name} = {value},");
    }

    public void Dispose()
    {
        _writer.Indent--;
        _writer.WriteLine("};");
    }
}