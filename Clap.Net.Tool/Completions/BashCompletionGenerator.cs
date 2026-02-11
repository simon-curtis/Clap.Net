using System.Text;

namespace Clap.Net.Tool.Completions;

/// <summary>
/// Generates Bash completion scripts using bash-completion framework.
/// </summary>
public class BashCompletionGenerator : CompletionBuilderBase
{
    public override string ShellName => "bash";
    public override string FileExtension => ".bash";

    public override string Generate(CompletionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.CommandName))
            throw new System.ArgumentException("Command name cannot be null or empty", nameof(context));

        var sb = new StringBuilder();
        var funcName = $"_{SanitizeFunctionName(context.CommandName)}_complete";

        // Header
        sb.AppendLine("#!/usr/bin/env bash");
        sb.AppendLine();
        sb.AppendLine($"# Bash completion script for {context.CommandName}");
        if (!string.IsNullOrEmpty(context.About))
        {
            sb.AppendLine($"# {context.About}");
        }
        sb.AppendLine("#");
        sb.AppendLine("# To enable completions, source this file:");
        sb.AppendLine($"#   source {context.CommandName}-completion.bash");
        sb.AppendLine("#");
        sb.AppendLine("# Or install system-wide:");
        sb.AppendLine($"#   sudo cp {context.CommandName}-completion.bash /etc/bash_completion.d/");
        sb.AppendLine();

        // Main completion function
        sb.AppendLine($"{funcName}() {{");
        sb.AppendLine("    local cur prev words cword");
        sb.AppendLine("    _init_completion || return");
        sb.AppendLine();

        // Global options
        var globalOpts = BuildOptionsList(context.Options);
        if (!string.IsNullOrEmpty(globalOpts))
        {
            sb.AppendLine($"    local global_opts=\"{globalOpts}\"");
        }

        // Subcommands list
        if (context.SubCommands.Any())
        {
            var subCommandNames = string.Join(" ", context.SubCommands.Select(s => s.Name));
            sb.AppendLine($"    local subcommands=\"{subCommandNames}\"");
            sb.AppendLine();

            // Detect active subcommand
            sb.AppendLine("    # Detect active subcommand");
            sb.AppendLine("    local subcommand=\"\"");
            sb.AppendLine("    local subcommand_index=0");
            sb.AppendLine("    for ((i = 1; i < cword; i++)); do");
            sb.AppendLine("        if [[ \" $subcommands \" =~ \" ${words[i]} \" ]]; then");
            sb.AppendLine("            subcommand=\"${words[i]}\"");
            sb.AppendLine("            subcommand_index=$i");
            sb.AppendLine("            break");
            sb.AppendLine("        fi");
            sb.AppendLine("    done");
            sb.AppendLine();

            // Subcommand-specific completions
            sb.AppendLine("    # Subcommand-specific completions");
            sb.AppendLine("    case \"$subcommand\" in");

            foreach (var subCmd in context.SubCommands)
            {
                GenerateSubCommandCase(sb, subCmd, 8);
            }

            sb.AppendLine("    esac");
            sb.AppendLine();
        }

        // Top-level completions
        sb.AppendLine("    # Top-level completions");
        sb.AppendLine("    if [[ \"$cur\" == -* ]]; then");

        if (!string.IsNullOrEmpty(globalOpts))
        {
            sb.AppendLine("        # Complete options");
            sb.AppendLine($"        COMPREPLY=( $(compgen -W \"$global_opts\" -- \"$cur\") )");
        }

        sb.AppendLine("        return 0");
        sb.AppendLine("    fi");

        if (context.SubCommands.Any())
        {
            sb.AppendLine();
            sb.AppendLine("    # Complete subcommands");
            sb.AppendLine("    COMPREPLY=( $(compgen -W \"$subcommands\" -- \"$cur\") )");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Register completion
        sb.AppendLine($"complete -F {funcName} {context.CommandName}");

        return sb.ToString();
    }

    private void GenerateSubCommandCase(StringBuilder sb, CompletionSubCommand subCmd, int indent)
    {
        var indentStr = new string(' ', indent);
        sb.AppendLine($"{indentStr}{subCmd.Name})");

        var subCmdOpts = BuildOptionsList(subCmd.Options);
        if (!string.IsNullOrEmpty(subCmdOpts))
        {
            sb.AppendLine($"{indentStr}    local opts=\"{subCmdOpts}\"");
            sb.AppendLine($"{indentStr}    if [[ \"$cur\" == -* ]]; then");
            sb.AppendLine($"{indentStr}        COMPREPLY=( $(compgen -W \"$opts\" -- \"$cur\") )");
            sb.AppendLine($"{indentStr}        return 0");
            sb.AppendLine($"{indentStr}    fi");
        }

        if (subCmd.SubCommands.Any())
        {
            var nestedNames = string.Join(" ", subCmd.SubCommands.Select(s => s.Name));
            sb.AppendLine($"{indentStr}    local nested_subcommands=\"{nestedNames}\"");
            sb.AppendLine($"{indentStr}    COMPREPLY=( $(compgen -W \"$nested_subcommands\" -- \"$cur\") )");
        }

        sb.AppendLine($"{indentStr}    return 0");
        sb.AppendLine($"{indentStr}    ;;");
    }
}
