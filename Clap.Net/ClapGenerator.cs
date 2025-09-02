using System;
using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Clap.Net.Generators;
using Clap.Net.Models;
using Clap.Net.Providers;
using Clap.Net.Serialisation;
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
            if (commandModel?.IsCliCommand is true)
                EmitCliJsonSchema(spc, commandModel, compilation, assemblyVersion, configOptions);
        }

        foreach (var subCommand in subCommandModels)
            GenerateSubCommandModelCode(spc, subCommand);
    }

    private static void EmitCliJsonSchema(
        SourceProductionContext context,
        CommandModel commandModel,
        Compilation compilation,
        string assemblyVersion,
        AnalyzerConfigOptionsProvider configOptions)
    {
        try
        {
            var outputPath = GetOutputPath(configOptions);
            var title = commandModel.Name ?? compilation.AssemblyName ?? "UnknownAssembly";

            var json = new SimpleJsonBuilder()
                .StartObject()
                .AddProperty("schema", "https://json-schema.org/draft/2020-12/schema")
                .AddProperty("opencli", "0.1");

            var info = json.StartObject("info");

            info.AddProperty("title", title);
            if (commandModel.About is not null)
                info.AddProperty("description", commandModel.About);

            info.AddProperty("version", commandModel.Version ?? assemblyVersion);
            info.EndObject();

            var jsonFilePath = Path.Combine(outputPath, $"{title}-cli.json");

            Directory.CreateDirectory(Path.GetDirectoryName(jsonFilePath)!);
            File.WriteAllText(jsonFilePath, json.EndObject().ToString());
        }
        catch (Exception ex)
        {
            // Report diagnostic instead of throwing
            var descriptor = new DiagnosticDescriptor(
                "CLI001",
                "CLI JSON Schema Generation Failed",
                "Failed to generate CLI JSON schema: {0}",
                "CLI",
                DiagnosticSeverity.Warning,
                isEnabledByDefault: true);

            context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None, ex.Message));
        }
    }

    private static string GetOutputPath(AnalyzerConfigOptionsProvider configOptions)
    {
        // Try to get project directory from MSBuild properties
        if (configOptions.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var projectDir))
        {
            return projectDir;
        }

        // Alternative: try ProjectDir
        if (configOptions.GlobalOptions.TryGetValue("build_property.ProjectDir", out var projDir))
        {
            return projDir;
        }

        // Fallback to current directory
        return Directory.GetCurrentDirectory();
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

        SubCommandCodeGenerator.GenerateSourceCode(writer, syntaxBuilder, subCommand);

        writer.Flush();
        spc.AddSource(
            $"{subCommand.Symbol.Name}.ParseMethod.g.cs",
            SourceText.From(textWriter.ToString(), Encoding.UTF8));
    }
}