using System.CodeDom.Compiler;

namespace Clap.Net.Extensions;

public static class IndentedTextWriterExtensions
{
    public static void WriteMultiLine(this IndentedTextWriter writer, string multiline)
    {
        foreach (var line in multiline.Split('\n'))
            writer.WriteLine(line.TrimEnd());
    }
}