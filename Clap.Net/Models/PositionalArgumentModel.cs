using Microsoft.CodeAnalysis;

namespace Clap.Net.Models;

internal record PositionalArgumentModel(
    ISymbol Symbol,
    ITypeSymbol MemberType,
    string VariableName,
    string? DefaultValue,
    string? Help,
    bool Required,
    int Index,
    ITypeSymbol? ValueParser = null,
    System.Collections.Immutable.ImmutableArray<AttributeData> ValidationAttributes = default
) : ArgumentModel(Symbol, MemberType, VariableName, Required, DefaultValue);