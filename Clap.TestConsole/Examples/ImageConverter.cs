using Clap.Net;

namespace Clap.TestConsole.Examples;

[Command(Name = "image-converter", Summary = "My app is pretty cool!")]
public partial class ImageConverter
{
    [Arg(Description = "The path of the image to convert")]
    public required string Path { get; init; }

    [Arg(Description = "The destination path of the converted image (default: <file-name>.[new-ext])", Last = true)]
    public string? DestinationPath { get; init; }

    [Arg(ShortName = 'e', LongName = "extension")]
    public string? Extension { get; init; }

    [Arg(ShortName = 'v', LongName= "verbose")]
    public bool Verbose { get; set; }

    [Command(SubCommand = true)]
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
        [Arg(Description = "The url to publish the new image to")]
        public required string[] UploadUrl { get; init; }
    }
}