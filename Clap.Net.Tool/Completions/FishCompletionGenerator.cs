using System.Text;

namespace Clap.Net.Tool.Completions;

/// <summary>
/// Generates Fish shell completion scripts.
/// </summary>
public class FishCompletionGenerator : CompletionBuilderBase
{
    public override string ShellName => "fish";
    public override string FileExtension => ".fish";

    public override string Generate(CompletionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.CommandName))
            throw new System.ArgumentException("Command name cannot be null or empty", nameof(context));

        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"# Fish completion script for {context.CommandName}");
        if (!string.IsNullOrEmpty(context.About))
        {
            sb.AppendLine($"# {context.About}");
        }
        sb.AppendLine("#");
        sb.AppendLine("# To enable completions, place this file in:");
        sb.AppendLine("#   ~/.config/fish/completions/" + context.CommandName + ".fish");
        sb.AppendLine("#");
        sb.AppendLine("# Or install system-wide:");
        sb.AppendLine("#   /usr/share/fish/vendor_completions.d/" + context.CommandName + ".fish");
        sb.AppendLine();

        // Helper function to check if no subcommand has been seen
        if (context.SubCommands.Any())
        {
            var subCommandNames = string.Join(" ", context.SubCommands.Select(s => s.Name));
            sb.AppendLine("# Helper function to check if no subcommand has been given");
            sb.AppendLine($"function __fish_{SanitizeFunctionName(context.CommandName)}_no_subcommand");
            sb.AppendLine($"    not __fish_seen_subcommand_from {subCommandNames}");
            sb.AppendLine("end");
            sb.AppendLine();
        }

        // Generate subcommands (must come before options to work correctly)
        if (context.SubCommands.Any())
        {
            sb.AppendLine("# Subcommands");
            foreach (var subCmd in context.SubCommands)
            {
                var desc = EscapeDescription(subCmd.About, '\'');
                var condition = $"__fish_{SanitizeFunctionName(context.CommandName)}_no_subcommand";
                sb.AppendLine($"complete -c {context.CommandName} -f -n '{condition}' -a '{subCmd.Name}' -d '{desc}'");
            }
            sb.AppendLine();
        }

        // Global options
        if (context.Options.Any())
        {
            sb.AppendLine("# Global options");
            foreach (var opt in context.Options)
            {
                GenerateOptionCompletion(sb, context.CommandName, opt, null);
            }
            sb.AppendLine();
        }

        // Subcommand-specific options
        foreach (var subCmd in context.SubCommands)
        {
            if (subCmd.Options.Any())
            {
                sb.AppendLine($"# Options for '{subCmd.Name}' subcommand");
                foreach (var opt in subCmd.Options)
                {
                    GenerateOptionCompletion(sb, context.CommandName, opt, subCmd.Name);
                }
                sb.AppendLine();
            }

            // Nested subcommands (if any)
            if (subCmd.SubCommands.Any())
            {
                sb.AppendLine($"# Nested subcommands for '{subCmd.Name}'");
                foreach (var nested in subCmd.SubCommands)
                {
                    var desc = EscapeDescription(nested.About, '\'');
                    sb.AppendLine($"complete -c {context.CommandName} -f -n '__fish_seen_subcommand_from {subCmd.Name}' -a '{nested.Name}' -d '{desc}'");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private void GenerateOptionCompletion(StringBuilder sb, string commandName, CompletionOption opt, string? subCommand)
    {
        var condition = subCommand is not null
            ? $"__fish_seen_subcommand_from {subCommand}"
            : "";

        var parts = new List<string>
        {
            $"complete -c {commandName}"
        };

        // Add condition
        if (!string.IsNullOrEmpty(condition))
        {
            parts.Add($"-n '{condition}'");
        }

        // Short option
        if (opt.Short.HasValue)
        {
            parts.Add($"-s {opt.Short.Value}");
        }

        // Long option
        if (!string.IsNullOrEmpty(opt.Long))
        {
            parts.Add($"-l {opt.Long}");
        }

        // Description
        if (!string.IsNullOrEmpty(opt.Help))
        {
            var desc = EscapeDescription(opt.Help, '\'');
            parts.Add($"-d '{desc}'");
        }

        // Flags don't require arguments
        if (opt.IsFlag)
        {
            parts.Add("-f");
        }

        sb.AppendLine(string.Join(" ", parts));
    }
}
