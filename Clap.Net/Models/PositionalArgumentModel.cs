using Microsoft.CodeAnalysis;

namespace Clap.Net.Models;

internal record PositionalArgumentModel(
    ISymbol Symbol,
    ITypeSymbol MemberType,
    string VariableName,
    string? DefaultValue,
    string? Help,
    bool Required,
    int Index
) : ArgumentModel(Symbol, MemberType, VariableName, Required, DefaultValue);