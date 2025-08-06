using Microsoft.CodeAnalysis;

namespace Clap.Net.Models;

internal record SubCommandArgumentModel(
    ISymbol Symbol,
    ITypeSymbol MemberType,
    string Name,
    bool IsRequired);