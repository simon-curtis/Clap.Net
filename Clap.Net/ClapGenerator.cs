using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Clap.Net.Generators;
using Clap.Net.Models;
using Clap.Net.Providers;
using Clap.Net.Serialisation;
using Clap.Net.Validation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

// ReSharper disable LoopCanBeConvertedToQuery

namespace Clap.Net;

[Generator(LanguageNames.CSharp)]
public class ClapGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var commandCandidates = CommandModelSyntaxProvider.RegisterSyntaxProvider(context.SyntaxProvider);
        var subCommandCandidates = SubCommandSyntaxProvider.RegisterSyntaxProvider(context.SyntaxProvider);

        var combined = commandCandidates.Collect()
            .Combine(subCommandCandidates.Collect())
            .Combine(context.CompilationProvider)
            .Combine(context.AnalyzerConfigOptionsProvider);

        context.RegisterSourceOutput(
            combined, static (spc, source) =>
            {
                var (((commandModels, subCommandModels), compilation), configOptions) = source;
                RegisterSourceOutput(spc, commandModels, subCommandModels, compilation, configOptions);
            });
    }

    private static void RegisterSourceOutput(
        SourceProductionContext spc,
        ImmutableArray<CommandModel?> commandModels,
        ImmutableArray<SubCommandModel> subCommandModels,
        Compilation compilation,
        AnalyzerConfigOptionsProvider configOptions)
    {
        var assemblyVersion = compilation.Assembly.Identity.Version.ToString();

        foreach (var commandModel in commandModels)
        {
            GenerateCommandModelCode(spc, commandModel, subCommandModels, assemblyVersion);
        }

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

        // Validate custom parsers before generating code
        ValidateCustomParsers(spc, commandModel);

        // Validate command model structure and arguments
        CommandValidator.ValidateCommandModel(spc, commandModel);

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

        SubCommandCodeGenerator.GenerateSourceCode(writer, syntaxBuilder, subCommand);

        writer.Flush();
        spc.AddSource(
            $"{subCommand.Symbol.Name}.ParseMethod.g.cs",
            SourceText.From(textWriter.ToString(), Encoding.UTF8));
    }

    private static void ValidateCustomParsers(SourceProductionContext context, CommandModel commandModel)
    {
        foreach (var argument in commandModel.Arguments)
        {
            ITypeSymbol? valueParser = argument switch
            {
                NamedArgumentModel named => named.ValueParser,
                PositionalArgumentModel positional => positional.ValueParser,
                _ => null
            };

            if (valueParser is null)
                continue;

            // Check if the parser type has a static Parse method
            var parseMethod = valueParser.GetMembers("Parse")
                .OfType<IMethodSymbol>()
                .FirstOrDefault(m =>
                    m.IsStatic &&
                    m.Parameters.Length == 1 &&
                    m.Parameters[0].Type.SpecialType == SpecialType.System_String);

            if (parseMethod is null)
            {
                var descriptor = new DiagnosticDescriptor(
                    "CLAP001",
                    "Invalid Custom Parser",
                    "Custom parser type '{0}' must have a static Parse(string) method for argument '{1}'",
                    "Clap.Net",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true);

                var location = argument.Symbol.Locations.FirstOrDefault() ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(
                    descriptor,
                    location,
                    valueParser.Name,
                    argument.Symbol.Name));
            }
        }
    }
}