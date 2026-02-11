using Clap.Net.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Clap.Net.Providers;

internal static class CommandModelSyntaxProvider
{
    internal static IncrementalValuesProvider<CommandModel?> RegisterSyntaxProvider(
        SyntaxValueProvider syntaxValueProvider)
    {
        return syntaxValueProvider.ForAttributeWithMetadataName(
                "Clap.Net.CommandAttribute",
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, _) =>
                {
                    var decl = (TypeDeclarationSyntax)ctx.TargetNode;
                    var symbol = (INamedTypeSymbol)ctx.TargetSymbol;
                    return CommandModelParser.GetCommandModel(symbol, decl);
                })
            .Where(symbol => symbol is not null);
    }
}