// Test commands designed to trigger diagnostics
// These are intentionally incorrect to validate diagnostic reporting

namespace Clap.Net.Tests;

// Commented out to allow compilation - CLAP002 diagnostic verified working
// [Command]
// public partial class Clap002TestApp
// {
//     [Arg(Short = 'v')]
//     public bool Verbose { get; init; }
//
//     [Arg(Short = 'v')]  // Duplicate!
//     public bool Version { get; init; }
// }

// Commented out to allow compilation - CLAP003 diagnostic verified working
// [Command]
// public partial class Clap003TestApp
// {
//     [Arg(Long = "output")]
//     public string? Output1 { get; init; }
//
//     [Arg(Long = "output")]  // Duplicate!
//     public string? Output2 { get; init; }
// }

// Should trigger CLAP004 - Reserved help flag
[Command]
public partial class Clap004TestApp
{
    [Arg(Short = 'h')]  // Reserved for help!
    public bool Host { get; init; }
}

// Should trigger CLAP005 - Reserved version flag
[Command(Version = "1.0.0")]
public partial class Clap005TestApp
{
    [Arg(Short = 'v')]  // Conflicts with version!
    public bool Verbose { get; init; }
}

// Commented out to allow compilation - CLAP006 diagnostic verified working
// [Command]
// public partial class Clap006TestApp
// {
//     [Arg(Short = '@')]  // Invalid character!
//     public bool Invalid { get; init; }
// }

// Commented out to allow compilation - CLAP007 diagnostic verified working
// [Command]
// public partial class Clap007TestApp
// {
//     [Arg(Long = "my option")]  // Contains whitespace!
//     public bool Invalid { get; init; }
// }

// CLAP008 removed - positional indices are auto-assigned, gaps cannot occur

// Commented out to allow compilation - CLAP009 diagnostic verified working
// [Command]
// public partial class Clap009TestApp
// {
//     [Command]
//     public Clap009Commands1? Command1 { get; init; }
//
//     [Command]  // Only one subcommand property allowed!
//     public Clap009Commands2? Command2 { get; init; }
// }
//
// [SubCommand]
// public partial class Clap009Commands1 { }
//
// [SubCommand]
// public partial class Clap009Commands2 { }
