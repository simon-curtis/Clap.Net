using Microsoft.CodeAnalysis;

namespace Clap.Net.Models;

internal record CommandModel(
    string Kind,
    string? Name,
    string? About,
    string? LongAbout,
    string? Version,
    INamedTypeSymbol Symbol,
    SubCommandArgumentModel? SubCommandArgumentModel,
    ArgumentModel[] Arguments
);