using Microsoft.CodeAnalysis;

namespace Clap.Net.Models;

internal abstract record ArgumentModel(
    ISymbol Symbol,
    ITypeSymbol MemberType,
    string VariableName,
    bool Required = false,
    string? DefaultValue = null);