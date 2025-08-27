using System.CodeDom.Compiler;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Clap.Net.Serialisation;

internal record TypeSyntaxBuilder : IDisposable
{
    private readonly IndentedTextWriter _writer;

    public TypeSyntaxBuilder(IndentedTextWriter writer, INamedTypeSymbol symbol)
    {
        _writer = writer;
        WriteAccessibilty(symbol.DeclaredAccessibility);
        _writer.WriteLine($" partial {symbol.TypeKind.ToString().ToLower()} {symbol.Name}");
        writer.WriteLine('{');
        _writer.Indent++;
    }

    public BlockSyntaxBuilder Method(
        BindingFlags bindingFlags,
        string returnType,
        string name,
        string[] parameters)
    {
        if ((bindingFlags & BindingFlags.Public) != 0)
            _writer.Write("public ");

        if ((bindingFlags & BindingFlags.Static) != 0)
            _writer.Write("static ");

        _writer.Write($"{returnType} {name}(");

        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0) _writer.Write(", ");
            _writer.Write(parameters[i]);
        }

        _writer.WriteLine(")");
        return new BlockSyntaxBuilder(_writer);
    }

    public void Dispose()
    {
        _writer.Indent--;
        _writer.WriteLine("}");
    }

    private void WriteAccessibilty(Accessibility accessibility)
    {
        if (accessibility == Accessibility.NotApplicable)
            return;

        _writer.Write(
            accessibility switch
            {
                Accessibility.Internal => "internal",
                Accessibility.Public => "public",
                Accessibility.Protected => "protected",
                _ => throw new ArgumentOutOfRangeException(nameof(accessibility), accessibility, null)
            });
    }
}