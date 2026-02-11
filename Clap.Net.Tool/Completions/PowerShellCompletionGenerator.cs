using System.Text;

namespace Clap.Net.Tool.Completions;

/// <summary>
/// Generates PowerShell completion scripts using ArgumentCompleter.
/// </summary>
public class PowerShellCompletionGenerator : CompletionBuilderBase
{
    public override string ShellName => "powershell";
    public override string FileExtension => ".ps1";

    public override string Generate(CompletionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.CommandName))
            throw new System.ArgumentException("Command name cannot be null or empty", nameof(context));

        var sb = new StringBuilder();

        // Header
        sb.AppendLine($"# PowerShell completion script for {context.CommandName}");
        if (!string.IsNullOrEmpty(context.About))
        {
            sb.AppendLine($"# {context.About}");
        }
        sb.AppendLine("#");
        sb.AppendLine("# To enable completions, dot-source this file in your PowerShell profile:");
        sb.AppendLine($"#   . {context.CommandName}-completion.ps1");
        sb.AppendLine("#");
        sb.AppendLine("# Or add to $PROFILE:");
        sb.AppendLine($"#   Add-Content $PROFILE \". `$PSScriptRoot\\{context.CommandName}-completion.ps1\"");
        sb.AppendLine();

        // Script block
        sb.AppendLine("$scriptBlock = {");
        sb.AppendLine("    param($wordToComplete, $commandAst, $cursorPosition)");
        sb.AppendLine();

        // Parse command elements to detect subcommand
        sb.AppendLine("    $commandElements = $commandAst.CommandElements");
        sb.AppendLine("    $subcommand = $null");
        sb.AppendLine();

        if (context.SubCommands.Any())
        {
            sb.AppendLine("    # Detect active subcommand");
            var subCommandNames = string.Join("', '", context.SubCommands.Select(s => s.Name));
            sb.AppendLine($"    $subcommands = @('{subCommandNames}')");
            sb.AppendLine("    for ($i = 1; $i -lt $commandElements.Count; $i++) {");
            sb.AppendLine("        $element = $commandElements[$i].ToString()");
            sb.AppendLine("        if ($subcommands -contains $element) {");
            sb.AppendLine("            $subcommand = $element");
            sb.AppendLine("            break");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        // Generate completions
        sb.AppendLine("    $completions = @()");
        sb.AppendLine();

        // Subcommand-specific completions
        if (context.SubCommands.Any())
        {
            sb.AppendLine("    if ($subcommand) {");
            sb.AppendLine("        switch ($subcommand) {");

            foreach (var subCmd in context.SubCommands)
            {
                sb.AppendLine($"            '{subCmd.Name}' {{");

                // Subcommand options
                foreach (var opt in subCmd.Options)
                {
                    GenerateCompletionResult(sb, opt, "                ");
                }

                // Nested subcommands
                foreach (var nested in subCmd.SubCommands)
                {
                    var desc = EscapeDescription(nested.About, '"');
                    sb.AppendLine($"                $completions += [System.Management.Automation.CompletionResult]::new('{nested.Name}', '{nested.Name}', 'ParameterValue', '{desc}')");
                }

                sb.AppendLine("            }");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("    else {");
            sb.AppendLine("        # Top-level completions");
            sb.AppendLine();

            // Global options
            foreach (var opt in context.Options)
            {
                GenerateCompletionResult(sb, opt, "        ");
            }

            // Subcommands
            foreach (var subCmd in context.SubCommands)
            {
                var desc = EscapeDescription(subCmd.About, '"');
                sb.AppendLine($"        $completions += [System.Management.Automation.CompletionResult]::new('{subCmd.Name}', '{subCmd.Name}', 'ParameterValue', '{desc}')");
            }

            sb.AppendLine("    }");
        }
        else
        {
            // No subcommands, just global options
            foreach (var opt in context.Options)
            {
                GenerateCompletionResult(sb, opt, "    ");
            }
        }

        sb.AppendLine();
        sb.AppendLine("    # Filter completions based on what user has typed");
        sb.AppendLine("    $completions | Where-Object { $_.CompletionText -like \"$wordToComplete*\" }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Register the completer
        sb.AppendLine($"Register-ArgumentCompleter -Native -CommandName {context.CommandName} -ScriptBlock $scriptBlock");

        return sb.ToString();
    }

    private void GenerateCompletionResult(StringBuilder sb, CompletionOption opt, string indent)
    {
        var desc = EscapeDescription(opt.Help, '"');

        if (opt.Short.HasValue)
        {
            var shortFlag = $"-{opt.Short.Value}";
            sb.AppendLine($"{indent}$completions += [System.Management.Automation.CompletionResult]::new('{shortFlag}', '{shortFlag}', 'ParameterName', '{desc}')");
        }

        if (!string.IsNullOrEmpty(opt.Long))
        {
            var longFlag = $"--{opt.Long}";
            sb.AppendLine($"{indent}$completions += [System.Management.Automation.CompletionResult]::new('{longFlag}', '{longFlag}', 'ParameterName', '{desc}')");
        }
    }
}
