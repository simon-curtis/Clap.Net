using Clap.Net;

namespace Clap.TestConsole.Examples;

/// <summary>
/// A fictional versioning CLI
/// </summary>
[Command(Name = "git")]
public partial struct Git
{
    [Arg(Short = 'u', Long = "username", Env = "GIT_USER_NAME", Help = "The user name to use for the commit")]
    public string? UserName { get; init; }

    [Arg(Short = 'v', Help = "Prints verbose output")]
    public bool Verbose { get; init; }

    [Command(Subcommand = true)]
    public required Commands Command { get; init; }
}

[SubCommand]
public abstract partial class Commands
{
    [Command(About = "Clone a repository into a new directory", LongAbout = """
        Clones a repository into a newly created directory, creates remote-tracking branches for each branch in the cloned repository (visible using git branch --remotes), and creates and checks out an initial branch that is forked from the cloned repository's currently active branch.
    
        After the clone, a plain git fetch without arguments will update all the remote-tracking branches, and a git pull without arguments will in addition merge the remote master branch into the current master branch, if any (this is untrue when --single-branch is given; see below).
    
        This default configuration is achieved by creating references to the remote branch heads under refs/remotes/origin and by initializing remote.origin.url and remote.origin.fetch configuration variables.
        """)]
    public sealed partial class Clone : Commands
    {
        [Arg(Long = "template-directory", Help = "Directory from which templates will be used")]
        public string? TemplateDirectory { get; init; }

        [Arg(Short = 'l', Long = "local", Help = "Clone from a local repository")]
        public bool Local { get; init; }

        [Arg(Short = 's', Long = "shared", Help = "Share the objects with the source repository")]
        public bool Shared { get; init; }

        [Arg(Long = "no-hardlinks", Help = "Force the cloning process from a repository on a local filesystem")]
        public bool NoHardLinks { get; init; }

        [Arg(Short = 'q', Long = "quiet", Help = "Operate quietly and do not report progress")]
        public bool Quiet { get; init; }

        [Arg(Short = 'n', Long = "no-checkout", Help = "No checkout of HEAD is performed after the clone is complete")]
        public bool NoCheckout { get; init; }

        [Arg(Long = "bare", Help = "Make a bare Git repository")]
        public bool Bare { get; init; }

        [Arg(Long = "mirror", Help = "Set up a mirror of the source repository")]
        public bool Mirror { get; init; }

        [Arg(Short = 'o', Long = "origin", Help = "Use custom name for the remote")]
        public string? Origin { get; init; }

        [Arg(Short = 'b', Long = "branch", Help = "Point HEAD to the specified branch")]
        public string? Branch { get; init; }

        [Arg(Short = 'u', Long = "upload-pack", Help = "Path to git-upload-pack on remote")]
        public string? UploadPack { get; init; }

        [Arg(Long = "reference", Help = "Reference another repository")]
        public string? ReferenceRepository { get; init; }

        [Arg(Long = "dissociate", Help = "Borrow the objects from reference repos only to reduce network transfer")]
        public bool Dissociate { get; init; }

        [Arg(Long = "separate-git-dir", Help = "Separate git dir from working tree")]
        public string? GitDir { get; init; }

        [Arg(Long = "depth", Help = "Create a shallow clone with specified depth")]
        public int? Depth { get; init; }

        [Arg(Long = "no-single-branch", Help = "Fetch all branches")]
        public bool? NoSingleBranch { get; init; }

        [Arg(Long = "single-branch", Help = "Clone only one branch")]
        public bool? SingleBranch { get; init; }

        [Arg(Long = "no-tags", Help = "Don't clone any tags")]
        public bool? NoTags { get; init; }

        [Arg(Long = "tags", Help = "Clone with all tags")]
        public bool? Tags { get; init; }

        [Arg(Long = "recurse-submodules", Help = "Initialize and clone submodules")]
        public string[]? RecusiveSubmodules { get; init; }

        [Arg(Long = "no-shallow-submodules", Help = "Disable shallow submodules")]
        public bool? NoShallowSubmodules { get; init; }

        [Arg(Long = "shallow-submodules", Help = "Make submodules shallow")]
        public bool? ShallowSubmodules { get; init; }

        [Arg(Long = "no-remote-submodules", Help = "Do not use remote submodules")]
        public bool? NoRemoteSubmodules { get; init; }

        [Arg(Long = "remote-submodules", Help = "Use remote submodules")]
        public bool? RemoteSubmodules { get; init; }

        [Arg(Long = "jobs", Help = "Number of parallel submodule clones")]
        public int? Jobs { get; init; }

        [Arg(Long = "sparse", Help = "Initialize sparse-checkout")]
        public bool? Sparse { get; init; }

        [Arg(Long = "no-reject-shallow", Help = "Allow fetching from shallow clone")]
        public bool? NoRejectShallow { get; init; }

        [Arg(Long = "reject-shallow", Help = "Reject shallow repository cloning")]
        public bool? RejectShallow { get; init; }

        [Arg(Long = "filter", Help = "Object filtering")]
        public string? FilterSpec { get; init; }

        [Arg(Long = "also-filter-submodules", Help = "Apply filtering to submodules")]
        public bool? AlsoFitlerSubmodules { get; init; }

        public required string Repository { get; init; }
        public string? Directory { get; init; }
    }

    [Command(Name = "status", About = "Show the working tree status")]
    public sealed partial class StatusCommand : Commands;

    [Command(About = "Add file contents to the index")]
    public sealed partial class Add : Commands
    {
        public required string[] Paths { get; init; }
    }

    [Command(About = "Show changes between commits, commit and working tree, etc")]
    public sealed partial class Diff : Commands
    {
        public required string Base { get; init; }
        public required string Head { get; init; }
        public required string Path { get; init; }
    }
}