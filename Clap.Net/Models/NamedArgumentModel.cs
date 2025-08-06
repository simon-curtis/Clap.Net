using Microsoft.CodeAnalysis;

namespace Clap.Net.Models;

internal record NamedArgumentModel(
    ISymbol Symbol,
    ITypeSymbol MemberType,
    string VariableName,
    string? DefaultValue,
    string? Help,
    char? Short,
    string? Long,
    string? Env,
    bool? Negation,
    ArgAction Action,
    bool Required
) : ArgumentModel(Symbol, MemberType, VariableName, Required, DefaultValue);