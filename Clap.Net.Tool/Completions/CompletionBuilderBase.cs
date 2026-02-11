using System.Text.RegularExpressions;

namespace Clap.Net.Tool.Completions;

/// <summary>
/// Base class with shared utilities for completion generators.
/// </summary>
public abstract class CompletionBuilderBase : ICompletionGenerator
{
    public abstract string Generate(CompletionContext context);
    public abstract string ShellName { get; }
    public abstract string FileExtension { get; }

    /// <summary>
    /// Escape special characters in description text for shell consumption.
    /// </summary>
    protected static string EscapeDescription(string? description, char quoteChar = '\'')
    {
        if (string.IsNullOrEmpty(description))
            return string.Empty;

        var escaped = description;

        // Replace the quote character with escaped version
        if (quoteChar == '\'')
        {
            // In single quotes, we need to end the quote, escape, and restart
            escaped = escaped.Replace("'", "'\\''");
        }
        else if (quoteChar == '"')
        {
            // In double quotes, escape backslash and quote
            escaped = escaped.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        // Remove newlines and excessive whitespace
        escaped = Regex.Replace(escaped, @"\s+", " ").Trim();

        return escaped;
    }

    /// <summary>
    /// Sanitize a string to be a valid shell function name.
    /// Replaces non-alphanumeric characters with underscores.
    /// </summary>
    protected static string SanitizeFunctionName(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_]", "_");

        // Ensure it doesn't start with a number
        if (char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;

        return sanitized;
    }

    /// <summary>
    /// Check if a string is a valid identifier (alphanumeric + underscore + hyphen).
    /// </summary>
    protected static bool IsValidIdentifier(string name)
    {
        return !string.IsNullOrEmpty(name) && Regex.IsMatch(name, @"^[a-zA-Z0-9_-]+$");
    }

    /// <summary>
    /// Collect all subcommand names recursively (space-separated path).
    /// </summary>
    protected static List<string> CollectSubCommandNames(List<CompletionSubCommand> subCommands, string prefix = "")
    {
        var names = new List<string>();

        foreach (var subCmd in subCommands)
        {
            var fullName = string.IsNullOrEmpty(prefix) ? subCmd.Name : $"{prefix} {subCmd.Name}";
            names.Add(fullName);

            // Recursively collect nested subcommands
            if (subCmd.SubCommands.Any())
            {
                names.AddRange(CollectSubCommandNames(subCmd.SubCommands, fullName));
            }
        }

        return names;
    }

    /// <summary>
    /// Build a space-separated list of option flags (e.g., "-v --verbose -h --help").
    /// </summary>
    protected static string BuildOptionsList(List<CompletionOption> options)
    {
        var flags = new List<string>();

        foreach (var opt in options)
        {
            if (opt.Short.HasValue)
                flags.Add($"-{opt.Short.Value}");
            if (!string.IsNullOrEmpty(opt.Long))
                flags.Add($"--{opt.Long}");
        }

        return string.Join(" ", flags);
    }
}
