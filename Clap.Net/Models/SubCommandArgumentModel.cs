using Microsoft.CodeAnalysis;

namespace Clap.Net.Models;

internal record SubCommandArgumentModel(
    ISymbol Symbol,
    ITypeSymbol MemberType,
    bool IsRequired,
    string? DefaultValue);