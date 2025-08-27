using Microsoft.CodeAnalysis;

namespace Clap.Net.Models;

internal record SubCommandModel(
    ISymbol Symbol, 
    List<CommandModel> Commands);