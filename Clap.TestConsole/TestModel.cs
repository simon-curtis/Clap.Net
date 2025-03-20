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
    [Command(Name = "status")]
    public sealed partial class Status : Commands;

    [Command(Name = "add")]
    public sealed partial class Add : Commands
    {
        
        public required string[] Paths { get; init; }
    }

    [Command]
    public sealed partial class Diff : Commands
    {
        public required string Base { get; init; }
        public required string Head { get; init; }

        [Arg(Last = true)] 
        public required string Path { get; init; }
    }
}