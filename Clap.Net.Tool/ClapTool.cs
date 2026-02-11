namespace Clap.Net.Tool;

[Command(Name = "clap", About = "Clap.Net CLI tool for generating OpenCLI JSON schemas and shell completions", Version = "1.0.0")]
public partial class ClapTool
{
    [Arg(Short = 'a', Long = "assembly", Help = "Path to the assembly containing Clap.Net commands")]
    public required string AssemblyPath { get; init; }

    [Arg(Short = 'o', Long = "output", Help = "Output file path (default: <assembly-name>-cli.json or shell-specific default)")]
    public string? OutputPath { get; init; }

    [Arg(Short = 'f', Long = "format", Help = "Output format: json, bash, zsh, fish, powershell (default: json)")]
    public string? Format { get; init; }

    [Arg(Long = "command-name", Help = "Override command name in generated completions (default: inferred from assembly)")]
    public string? CommandName { get; init; }
}
