using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Clap.Net.Models;

internal record SubCommandModel(
    INamedTypeSymbol Symbol, 
    List<CommandModel> Commands);