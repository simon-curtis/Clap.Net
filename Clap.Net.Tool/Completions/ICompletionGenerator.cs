namespace Clap.Net.Tool.Completions;

/// <summary>
/// Interface for shell completion script generators.
/// </summary>
public interface ICompletionGenerator
{
    /// <summary>
    /// Generate a completion script for the given command context.
    /// </summary>
    string Generate(CompletionContext context);

    /// <summary>
    /// The name of the shell (e.g., "bash", "zsh").
    /// </summary>
    string ShellName { get; }

    /// <summary>
    /// The file extension for the completion script (e.g., ".bash", ".fish").
    /// </summary>
    string FileExtension { get; }
}
