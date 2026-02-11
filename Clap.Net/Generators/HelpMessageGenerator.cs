using System.Collections.Generic;
using System.Linq;
using System.Text;
using Clap.Net.Extensions;
using Clap.Net.Models;

namespace Clap.Net.Generators;

internal static class HelpMessageGenerator
{
    public static string GenerateHelpMessage(
        CommandModel commandModel,
        SubCommandModel? subCommand,
        NamedArgumentModel[] namedArguments,
        PositionalArgumentModel[] positionalArgs)
    {
        var sb = new StringBuilder();

        var about = commandModel.LongAbout ?? commandModel.About;
        if (!string.IsNullOrEmpty(about))
        {
            sb.AppendLine(about!.Trim());
            sb.AppendLine();
        }

        sb.Append("Usage: {{EXECUTABLE_NAME}}");

        if (commandModel is { IsSubCommand: true, Name: not null })
            sb.Append($" {commandModel.Name}");

        if (namedArguments.Any())
            sb.Append(" [OPTIONS]");

        foreach (var positional in positionalArgs)
            sb.Append($" <{positional.Symbol.Name}>");
        sb.AppendLine();

        var table = new List<string?[]>();

        if (positionalArgs.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Arguments:");
            foreach (var positional in positionalArgs)
                table.Add([$"<{positional.Symbol.Name}>", positional.Help]);

            var maxArgLength = table.Max(o => o[0]?.Length ?? 0);
            foreach (var row in table)
                sb.AppendLine($"  {row[0]?.PadRight(maxArgLength)}  {row[1]}");

            table.Clear();
        }

        foreach (var option in namedArguments)
        {
            var names = option switch
            {
                { Short: { } shortName, Long: { } longName } => $"-{shortName}, --{longName}",
                { Short: { } shortName } => $"-{shortName}",
                { Long: { } longName } => $"--{longName}",
                _ => $"{option.Symbol.Name.ToSnakeCase()}"
            };

            table.Add([names, option.Help]);
        }

        table.Add(["-h, --help", "Shows this help message"]);

        var maxColumnLength = table.Max(o => o[0]?.Length ?? 0);

        sb.AppendLine();
        sb.AppendLine("Options:");
        foreach (var row in table)
            sb.AppendLine($"  {row[0]?.PadRight(maxColumnLength)}  {row[1]}");
        table.Clear();

        if (subCommand is { Commands.Count: > 0 })
        {
            foreach (var command in subCommand.Commands)
                table.Add([command.Name, command.About]);

            maxColumnLength = table.Max(o => o[0]?.Length ?? 0);

            sb.AppendLine();
            sb.AppendLine("Commands:");
            foreach (var row in table)
                sb.AppendLine($"  {row[0]?.PadRight(maxColumnLength)}  {row[1]}");
        }

        return sb.ToString();
    }
}
