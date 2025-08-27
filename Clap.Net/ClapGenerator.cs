using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Text;
using Clap.Net.Generators;
using Clap.Net.Models;
using Clap.Net.Providers;
using Clap.Net.Serialisation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

// ReSharper disable LoopCanBeConvertedToQuery

namespace Clap.Net;

[Generator]
public class ClapGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var compilerInfoProvider = context
            .CompilationProvider
            .Select((c, _) => c.Assembly.Identity.Version.ToString());

        var commandCandidates = CommandModelSyntaxProvider.RegisterSyntaxProvider(context.SyntaxProvider);
        var subCommandCandidates = SubCommandSyntaxProvider.RegisterSyntaxProvider(context.SyntaxProvider);

        var combined = commandCandidates.Collect()
            .Combine(subCommandCandidates.Collect())
            .Combine(compilerInfoProvider);

        context.RegisterSourceOutput(combined, RegisterSourceOutput);
    }

    private static void RegisterSourceOutput(
        SourceProductionContext spc,
        ((ImmutableArray<CommandModel?> Left, ImmutableArray<SubCommandModel> Right) Left, string Right) tuple)
    {
        var ((commandModels, subCommandModels), version) = tuple;

        foreach (var commandModel in commandModels)
            GenerateCommandModelCode(spc, commandModel, subCommandModels, version);

        foreach (var subCommand in subCommandModels)
            GenerateSubCommandModelCode(spc, subCommand);
    }

    private static void GenerateCommandModelCode(
        SourceProductionContext spc,
        CommandModel? commandModel,
        ImmutableArray<SubCommandModel> subCommandModels,
        string version)
    {
        if (commandModel is null) return;

        using var textWriter = new StringWriter();
        var writer = new IndentedTextWriter(textWriter);
        var syntaxBuilder = new SyntaxBuilder(writer);

        CommandCodeGenerator.GenerateSourceCode(writer, syntaxBuilder, commandModel, subCommandModels, version);

        writer.Flush();
        spc.AddSource(
            $"{commandModel.Symbol.Name}.ParseMethod.g.cs",
            SourceText.From(textWriter.ToString(), Encoding.UTF8));
    }

    private static void GenerateSubCommandModelCode(SourceProductionContext spc, SubCommandModel subCommand)
    {
        using var textWriter = new StringWriter();
        var writer = new IndentedTextWriter(textWriter);
        var syntaxBuilder = new SyntaxBuilder(writer);

        SubCommandCodeGenerator.GenerateSourceCode(writer, subCommand);

        writer.Flush();
        spc.AddSource(
            $"{subCommand.Symbol.Name}.ParseMethod.g.cs",
            SourceText.From(textWriter.ToString(), Encoding.UTF8));
    }
}