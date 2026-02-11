using System.Text;

namespace Clap.Net.Tool.Completions;

/// <summary>
/// Generates Zsh completion scripts using the zsh completion system.
/// </summary>
public class ZshCompletionGenerator : CompletionBuilderBase
{
    public override string ShellName => "zsh";
    public override string FileExtension => ""; // Zsh uses _commandname convention

    public override string Generate(CompletionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.CommandName))
            throw new System.ArgumentException("Command name cannot be null or empty", nameof(context));

        var sb = new StringBuilder();
        var funcName = $"_{SanitizeFunctionName(context.CommandName)}";

        // Header
        sb.AppendLine($"#compdef {context.CommandName}");
        sb.AppendLine();
        sb.AppendLine($"# Zsh completion script for {context.CommandName}");
        if (!string.IsNullOrEmpty(context.About))
        {
            sb.AppendLine($"# {context.About}");
        }
        sb.AppendLine("#");
        sb.AppendLine("# To enable completions, place this file in your $fpath with the name:");
        sb.AppendLine($"#   _{context.CommandName}");
        sb.AppendLine("#");
        sb.AppendLine("# Common locations:");
        sb.AppendLine("#   /usr/local/share/zsh/site-functions/_" + context.CommandName);
        sb.AppendLine("#   ~/.zsh/completions/_" + context.CommandName);
        sb.AppendLine();

        // Main completion function
        sb.AppendLine($"{funcName}() {{");
        sb.AppendLine("    local context state state_descr line");
        sb.AppendLine("    typeset -A opt_args");
        sb.AppendLine();

        // Build global arguments
        sb.AppendLine("    _arguments -C \\");

        // Add global options
        foreach (var opt in context.Options)
        {
            GenerateOptionSpec(sb, opt, "        ");
        }

        // Add subcommand handling if present
        if (context.SubCommands.Any())
        {
            sb.AppendLine("        '1:subcommand:->subcommand' \\");
            sb.AppendLine("        '*::arg:->args'");
            sb.AppendLine();

            // State handling
            sb.AppendLine("    case \"$state\" in");
            sb.AppendLine("        subcommand)");

            // Generate subcommand descriptions
            sb.AppendLine("            local -a subcommands");
            sb.AppendLine("            subcommands=(");
            foreach (var subCmd in context.SubCommands)
            {
                var desc = EscapeDescription(subCmd.About, '\'');
                sb.AppendLine($"                '{subCmd.Name}:{desc}'");
            }
            sb.AppendLine("            )");
            sb.AppendLine($"            _describe '{context.CommandName} subcommands' subcommands");
            sb.AppendLine("            ;;");

            sb.AppendLine("        args)");
            sb.AppendLine("            case \"$line[1]\" in");

            // Generate subcommand-specific completions
            foreach (var subCmd in context.SubCommands)
            {
                GenerateSubCommandSpec(sb, subCmd, "                ");
            }

            sb.AppendLine("            esac");
            sb.AppendLine("            ;;");
            sb.AppendLine("    esac");
        }
        else
        {
            // No subcommands, just close _arguments
            sb.Remove(sb.Length - 3, 3); // Remove trailing " \"
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"{funcName} \"$@\"");

        return sb.ToString();
    }

    private void GenerateOptionSpec(StringBuilder sb, CompletionOption opt, string indent)
    {
        var specs = new List<string>();

        // Build the option spec: '(-s --long)'{-s,--long}'[description]'
        if (opt.Short.HasValue && !string.IsNullOrEmpty(opt.Long))
        {
            // Both short and long
            var exclusion = $"(-{opt.Short.Value} --{opt.Long})";
            var names = $"{{-{opt.Short.Value},--{opt.Long}}}";
            var desc = EscapeDescription(opt.Help, '\'');
            sb.Append($"{indent}'{exclusion}{names}[{desc}]");
        }
        else if (opt.Short.HasValue)
        {
            // Short only
            var desc = EscapeDescription(opt.Help, '\'');
            sb.Append($"{indent}'-{opt.Short.Value}[{desc}]");
        }
        else if (!string.IsNullOrEmpty(opt.Long))
        {
            // Long only
            var desc = EscapeDescription(opt.Help, '\'');
            sb.Append($"{indent}'--{opt.Long}[{desc}]");
        }

        // Add value placeholder if not a flag
        if (!opt.IsFlag)
        {
            sb.Append(":value:");
        }

        sb.AppendLine("' \\");
    }

    private void GenerateSubCommandSpec(StringBuilder sb, CompletionSubCommand subCmd, string indent)
    {
        sb.AppendLine($"{indent}{subCmd.Name})");

        if (subCmd.Options.Any())
        {
            sb.AppendLine($"{indent}    _arguments \\");
            foreach (var opt in subCmd.Options)
            {
                GenerateOptionSpec(sb, opt, $"{indent}        ");
            }
            // Remove trailing " \"
            sb.Remove(sb.Length - 3, 3);
            sb.AppendLine();
        }

        if (subCmd.SubCommands.Any())
        {
            // Nested subcommands (simple case - just list them)
            sb.AppendLine($"{indent}    local -a nested_subcommands");
            sb.AppendLine($"{indent}    nested_subcommands=(");
            foreach (var nested in subCmd.SubCommands)
            {
                var desc = EscapeDescription(nested.About, '\'');
                sb.AppendLine($"{indent}        '{nested.Name}:{desc}'");
            }
            sb.AppendLine($"{indent}    )");
            sb.AppendLine($"{indent}    _describe 'subcommands' nested_subcommands");
        }

        sb.AppendLine($"{indent}    ;;");
    }
}
