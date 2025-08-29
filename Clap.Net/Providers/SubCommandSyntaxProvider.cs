using System.Collections.Generic;
using System.Linq;
using Clap.Net.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Clap.Net.Providers;

internal static class SubCommandSyntaxProvider
{
    internal static IncrementalValuesProvider<SubCommandModel> RegisterSyntaxProvider(
        SyntaxValueProvider syntaxValueProvider)
    {
        return syntaxValueProvider.ForAttributeWithMetadataName(
                "Clap.Net.SubCommandAttribute",
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, _) =>
                {
                    var symbol = (INamedTypeSymbol)ctx.TargetSymbol;

                    var commands = new List<CommandModel>();
                    foreach (var nestedTypeSymbol in symbol.GetTypeMembers())
                    {
                        if (nestedTypeSymbol.DeclaredAccessibility != Accessibility.Public)
                            continue;

                        // get the syntax of the nested symbol
                        var typeDeclarationSyntax = nestedTypeSymbol.DeclaringSyntaxReferences
                            .OfType<TypeDeclarationSyntax>()
                            .FirstOrDefault();

                        if (CommandModelParser.GetCommandModel(nestedTypeSymbol, typeDeclarationSyntax) is { } model)
                            commands.Add(model);
                    }

                    return new SubCommandModel(symbol, commands);
                })
            .Where(symbol => symbol != null);
    }
}