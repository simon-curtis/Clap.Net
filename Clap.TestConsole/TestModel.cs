using Clap.Net;

namespace Clap.TestConsole;

[Command(Name = "git", Description = "A fictional versioning CLI")]
public partial class Git
{
    [Arg(ShortName = 'v', Description = "Prints verbose output")]
    public bool Verbose { get; init; }

    [Command(Subcommand = true)]
    public required Commands Command { get; init; }
}

[SubCommand]
public abstract partial class Commands
{
    [Command(Name = "status", Description = "Show the working tree status")]
    public sealed partial class StatusCommand : Commands;

    [Command(Description = "Add file contents to the index")]
    public sealed partial class Add : Commands
    {
        [Arg(Required = true)]
        public required string[] Paths { get; init; }
    }

    [Command(Description = "Show changes between commits, commit and working tree, etc")]
    public sealed partial class Diff : Commands
    {
        public required string Base { get; init; }
        public required string Head { get; init; }

        [Arg(Last = true)] 
        public required string Path { get; init; }
    }
}