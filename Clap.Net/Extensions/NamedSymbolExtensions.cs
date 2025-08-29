using Microsoft.CodeAnalysis;

namespace Clap.Net.Extensions;

public static class NamedSymbolExtensions
{
    internal static string GetTypeKeyword(this ITypeSymbol symbol)
    {
        return symbol.TypeKind switch
        {
            TypeKind.Class => symbol.IsRecord ? "record" : "class",
            TypeKind.Struct => symbol.IsRecord ? "record struct" : "struct", 
            TypeKind.Interface => "interface",
            TypeKind.Enum => "enum",
            TypeKind.Delegate => "delegate",
            _ => symbol.TypeKind.ToString().ToLower()
        };
    }
}