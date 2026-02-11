namespace Clap.Net.Tool.Completions;

/// <summary>
/// Represents the complete metadata for a command, used to generate shell completions.
/// </summary>
public record CompletionContext(
    string CommandName,
    string? About,
    List<CompletionOption> Options,
    List<CompletionPositional> Positionals,
    List<CompletionSubCommand> SubCommands
);

/// <summary>
/// Represents a named option/flag in a command.
/// </summary>
public record CompletionOption(
    char? Short,
    string? Long,
    string? Help,
    bool IsFlag,
    bool Required
);

/// <summary>
/// Represents a positional argument in a command.
/// </summary>
public record CompletionPositional(
    string Name,
    string? Help,
    int Index,
    bool Required
);

/// <summary>
/// Represents a subcommand with its own options and nested subcommands.
/// </summary>
public record CompletionSubCommand(
    string Name,
    string? About,
    List<CompletionOption> Options,
    List<CompletionPositional> Positionals,
    List<CompletionSubCommand> SubCommands
);
