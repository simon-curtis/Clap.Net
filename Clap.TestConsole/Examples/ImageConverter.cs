using Clap.Net;

namespace Clap.TestConsole.Examples;

[Command(Name = "image-converter", About = "My app is pretty cool!")]
public partial class ImageConverter
{
    [Arg(Help = "The path of the image to convert")]
    public required string Path { get; init; }

    [Arg(Help = "The destination path of the converted image (default: <file-name>.[new-ext])")]
    public string? DestinationPath { get; init; }

    [Arg(Short = 'e', Long = "extension")]
    public string? Extension { get; init; }

    [Arg(Short = 'v', Long= "verbose")]
    public bool Verbose { get; set; }

    [Command(Subcommand = true)]
    public ImageConverterCommands? Command { get; init; }
}

[SubCommand]
public partial class ImageConverterCommands 
{
    [Command]
    public partial class History : ImageConverterCommands;

    [Command]
    public partial class Publish : ImageConverterCommands 
    {
        [Arg(Help = "The url to publish the new image to")]
        public required string[] UploadUrl { get; init; }
    }
}