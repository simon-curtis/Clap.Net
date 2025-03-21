using Clap.Net;

namespace Clap.TestConsole.Examples;

[Command(Name = "git", Summary = "A fictional versioning CLI")]
public partial struct Git
{
    [Arg(ShortName = 'v', Description = "Prints verbose output")]
    public bool Verbose { get; init; }

    [Command(SubCommand = true)]
    public required Commands Command { get; init; }
}

[SubCommand]
public abstract partial class Commands
{
    [Command(Summary = "Clone a repository into a new directory", Description = """
        Clones a repository into a newly created directory, creates remote-tracking branches for each branch in the cloned repository (visible using git branch --remotes), and creates and checks out an initial branch that is forked from the cloned repository's currently active branch.
    
        After the clone, a plain git fetch without arguments will update all the remote-tracking branches, and a git pull without arguments will in addition merge the remote master branch into the current master branch, if any (this is untrue when --single-branch is given; see below).
    
        This default configuration is achieved by creating references to the remote branch heads under refs/remotes/origin and by initializing remote.origin.url and remote.origin.fetch configuration variables.
        """)]
    public sealed partial class Clone : Commands
    {
        [Arg(LongName = "template-directory", Description = "Directory from which templates will be used")]
        public string? TemplateDirectory { get; init; }

        [Arg(ShortName = 'l', LongName = "local", Description = "Clone from a local repository")]
        public bool Local { get; init; }

        [Arg(ShortName = 's', LongName = "shared", Description = "Share the objects with the source repository")]
        public bool Shared { get; init; }

        [Arg(LongName = "no-hardlinks", Description = "Force the cloning process from a repository on a local filesystem")]
        public bool NoHardLinks { get; init; }

        [Arg(ShortName = 'q', LongName = "quiet", Description = "Operate quietly and do not report progress")]
        public bool Quiet { get; init; }

        [Arg(ShortName = 'n', LongName = "no-checkout", Description = "No checkout of HEAD is performed after the clone is complete")]
        public bool NoCheckout { get; init; }

        [Arg(LongName = "bare", Description = "Make a bare Git repository")]
        public bool Bare { get; init; }

        [Arg(LongName = "mirror", Description = "Set up a mirror of the source repository")]
        public bool Mirror { get; init; }

        [Arg(ShortName = 'o', LongName = "origin", Description = "Use custom name for the remote")]
        public string? Origin { get; init; }

        [Arg(ShortName = 'b', LongName = "branch", Description = "Point HEAD to the specified branch")]
        public string? Branch { get; init; }

        [Arg(ShortName = 'u', LongName = "upload-pack", Description = "Path to git-upload-pack on remote")]
        public string? UploadPack { get; init; }

        [Arg(LongName = "reference", Description = "Reference another repository")]
        public string? ReferenceRepository { get; init; }

        [Arg(LongName = "dissociate", Description = "Borrow the objects from reference repos only to reduce network transfer")]
        public bool Dissociate { get; init; }

        [Arg(LongName = "separate-git-dir", Description = "Separate git dir from working tree")]
        public string? GitDir { get; init; }

        [Arg(LongName = "depth", Description = "Create a shallow clone with specified depth")]
        public int? Depth { get; init; }

        [Arg(LongName = "no-single-branch", Description = "Fetch all branches")]
        public bool? NoSingleBranch { get; init; }

        [Arg(LongName = "single-branch", Description = "Clone only one branch")]
        public bool? SingleBranch { get; init; }

        [Arg(LongName = "no-tags", Description = "Don't clone any tags")]
        public bool? NoTags { get; init; }

        [Arg(LongName = "tags", Description = "Clone with all tags")]
        public bool? Tags { get; init; }

        [Arg(LongName = "recurse-submodules", Description = "Initialize and clone submodules")]
        public string[]? RecusiveSubmodules { get; init; }

        [Arg(LongName = "no-shallow-submodules", Description = "Disable shallow submodules")]
        public bool? NoShallowSubmodules { get; init; }

        [Arg(LongName = "shallow-submodules", Description = "Make submodules shallow")]
        public bool? ShallowSubmodules { get; init; }

        [Arg(LongName = "no-remote-submodules", Description = "Do not use remote submodules")]
        public bool? NoRemoteSubmodules { get; init; }

        [Arg(LongName = "remote-submodules", Description = "Use remote submodules")]
        public bool? RemoteSubmodules { get; init; }

        [Arg(LongName = "jobs", Description = "Number of parallel submodule clones")]
        public int? Jobs { get; init; }

        [Arg(LongName = "sparse", Description = "Initialize sparse-checkout")]
        public bool? Sparse { get; init; }

        [Arg(LongName = "no-reject-shallow", Description = "Allow fetching from shallow clone")]
        public bool? NoRejectShallow { get; init; }

        [Arg(LongName = "reject-shallow", Description = "Reject shallow repository cloning")]
        public bool? RejectShallow { get; init; }

        [Arg(LongName = "filter", Description = "Object filtering")]
        public string? FilterSpec { get; init; }

        [Arg(LongName = "also-filter-submodules", Description = "Apply filtering to submodules")]
        public bool? AlsoFitlerSubmodules { get; init; }

        public required string Repository { get; init; }
        public string? Directory { get; init; }
    }

    [Command(Name = "status", Summary = "Show the working tree status")]
    public sealed partial class StatusCommand : Commands;

    [Command(Summary = "Add file contents to the index")]
    public sealed partial class Add : Commands
    {
        [Arg(Required = true)]
        public required string[] Paths { get; init; }
    }

    [Command(Summary = "Show changes between commits, commit and working tree, etc")]
    public sealed partial class Diff : Commands
    {
        public required string Base { get; init; }
        public required string Head { get; init; }

        [Arg(Last = true)]
        public required string Path { get; init; }
    }
}