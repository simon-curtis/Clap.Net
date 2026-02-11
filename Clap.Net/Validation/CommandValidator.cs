using System.Linq;
using Clap.Net.Models;
using Microsoft.CodeAnalysis;

namespace Clap.Net.Validation;

internal static class CommandValidator
{
    // Diagnostic descriptors (static readonly for performance)
    private static readonly DiagnosticDescriptor DuplicateShortFlag = new(
        "CLAP002",
        "Duplicate Short Flag",
        "Duplicate short flag '-{0}' found on arguments '{1}' and '{2}'",
        "Clap.Net",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateLongOption = new(
        "CLAP003",
        "Duplicate Long Option",
        "Duplicate long option '--{0}' found on arguments '{1}' and '{2}'",
        "Clap.Net",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ReservedHelpFlag = new(
        "CLAP004",
        "Reserved Help Flag",
        "Argument '{0}' uses reserved help flag '{1}'. This will override the built-in help functionality",
        "Clap.Net",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ReservedVersionFlag = new(
        "CLAP005",
        "Reserved Version Flag",
        "Argument '{0}' uses flag '{1}' which conflicts with the version flag for this command. Version will take precedence",
        "Clap.Net",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidShortFlag = new(
        "CLAP006",
        "Invalid Short Flag Character",
        "Invalid short flag character '{0}' on argument '{1}'. Short flags must be alphanumeric",
        "Clap.Net",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidLongOption = new(
        "CLAP007",
        "Invalid Long Option Name",
        "Invalid long option name '{0}' on argument '{1}'. Long options must be non-empty and cannot contain whitespace",
        "Clap.Net",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // CLAP008 removed - positional indices are auto-assigned sequentially, gaps cannot occur

    private static readonly DiagnosticDescriptor MultipleSubCommands = new(
        "CLAP009",
        "Multiple Subcommand Properties",
        "Command '{0}' defines multiple subcommand properties: '{1}' and '{2}'. Only one subcommand property is allowed",
        "Clap.Net",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static void ValidateCommandModel(SourceProductionContext context, CommandModel commandModel)
    {
        ValidateDuplicateFlags(context, commandModel);
        ValidateReservedFlags(context, commandModel);
        ValidateFlagCharacters(context, commandModel);
        // ValidatePositionalOrder - Not needed as indices are auto-assigned sequentially
        ValidateSubCommandCount(context, commandModel);
    }

    private static void ValidateDuplicateFlags(SourceProductionContext context, CommandModel commandModel)
    {
        var namedArgs = commandModel.Arguments.OfType<NamedArgumentModel>().ToArray();

        // Check for duplicate short flags
        var shortDuplicates = namedArgs
            .Where(a => a.Short.HasValue)
            .GroupBy(a => a.Short!.Value)
            .Where(g => g.Count() > 1);

        foreach (var group in shortDuplicates)
        {
            var args = group.ToArray();
            for (int i = 1; i < args.Length; i++)
            {
                var location = args[i].Symbol.Locations.FirstOrDefault() ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateShortFlag,
                    location,
                    group.Key,
                    args[0].Symbol.Name,
                    args[i].Symbol.Name));
            }
        }

        // Check for duplicate long options
        var longDuplicates = namedArgs
            .Where(a => !string.IsNullOrEmpty(a.Long))
            .GroupBy(a => a.Long!)
            .Where(g => g.Count() > 1);

        foreach (var group in longDuplicates)
        {
            var args = group.ToArray();
            for (int i = 1; i < args.Length; i++)
            {
                var location = args[i].Symbol.Locations.FirstOrDefault() ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateLongOption,
                    location,
                    group.Key,
                    args[0].Symbol.Name,
                    args[i].Symbol.Name));
            }
        }
    }

    private static void ValidateReservedFlags(SourceProductionContext context, CommandModel commandModel)
    {
        var namedArgs = commandModel.Arguments.OfType<NamedArgumentModel>().ToArray();

        // Check for conflicts with help flags (-h, --help)
        foreach (var arg in namedArgs)
        {
            if (arg.Short == 'h')
            {
                var location = arg.Symbol.Locations.FirstOrDefault() ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(
                    ReservedHelpFlag,
                    location,
                    arg.Symbol.Name,
                    "-h"));
            }

            if (arg.Long == "help")
            {
                var location = arg.Symbol.Locations.FirstOrDefault() ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(
                    ReservedHelpFlag,
                    location,
                    arg.Symbol.Name,
                    "--help"));
            }
        }

        // Check for conflicts with version flags (-v, --version) if Version is set
        if (!string.IsNullOrEmpty(commandModel.Version))
        {
            foreach (var arg in namedArgs)
            {
                if (arg.Short == 'v')
                {
                    var location = arg.Symbol.Locations.FirstOrDefault() ?? Location.None;
                    context.ReportDiagnostic(Diagnostic.Create(
                        ReservedVersionFlag,
                        location,
                        arg.Symbol.Name,
                        "-v"));
                }

                if (arg.Long == "version")
                {
                    var location = arg.Symbol.Locations.FirstOrDefault() ?? Location.None;
                    context.ReportDiagnostic(Diagnostic.Create(
                        ReservedVersionFlag,
                        location,
                        arg.Symbol.Name,
                        "--version"));
                }
            }
        }
    }

    private static void ValidateFlagCharacters(SourceProductionContext context, CommandModel commandModel)
    {
        var namedArgs = commandModel.Arguments.OfType<NamedArgumentModel>().ToArray();

        foreach (var arg in namedArgs)
        {
            // Validate short flag is alphanumeric
            if (arg.Short.HasValue && !char.IsLetterOrDigit(arg.Short.Value))
            {
                var location = arg.Symbol.Locations.FirstOrDefault() ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidShortFlag,
                    location,
                    arg.Short.Value,
                    arg.Symbol.Name));
            }

            // Validate long option is not empty/whitespace
            if (!string.IsNullOrEmpty(arg.Long))
            {
                if (string.IsNullOrWhiteSpace(arg.Long) || arg.Long.Any(char.IsWhiteSpace))
                {
                    var location = arg.Symbol.Locations.FirstOrDefault() ?? Location.None;
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidLongOption,
                        location,
                        arg.Long,
                        arg.Symbol.Name));
                }
            }
        }
    }


    private static void ValidateSubCommandCount(SourceProductionContext context, CommandModel commandModel)
    {
        // Find all properties/fields with [Command] attribute
        var subCommandProperties = commandModel.Symbol.GetMembers()
            .Where(m => m is IPropertySymbol or IFieldSymbol)
            .Where(m => m.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name == nameof(CommandAttribute)))
            .ToArray();

        if (subCommandProperties.Length > 1)
        {
            var location = subCommandProperties[0].Locations.FirstOrDefault() ?? Location.None;
            context.ReportDiagnostic(Diagnostic.Create(
                MultipleSubCommands,
                location,
                commandModel.Symbol.Name,
                subCommandProperties[0].Name,
                subCommandProperties[1].Name));
        }
    }
}
