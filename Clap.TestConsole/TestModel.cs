using Clap.Net;

namespace Clap.TestConsole;

[Command(Name = "myapp", Description = "Does cool things")]
public partial class TestModel
{
    [Positional(Index = 0, Description = "Input file")]
    public required string InputFile { get; init; }

    [Switch(LongName = "verbose", ShortName = 'v', Description = "Enable verbose logging")]
    public bool Verbose { get; init; }

    [Option(LongName = "file", ShortName = 'f', Description = "Config file path")]
    public string? ConfigFile { get; init; }

    [Option(LongName = "number", ShortName = 'n', Description = "Number of times")]
    public int? NumberOfTimes { get; init; }
}