```markdown
# Clap.Net

Clap.Net is an attempt to port clap-rs over to .NET, with almost feature-parity, to make source generated parsers for applications.

## Installation

```bash
dotnet add package Clap.Net
```

## Usage

Define your command-line interface using classes and properties. Use attributes to specify the command-line options and arguments.

```csharp
using Clap.Net;

namespace Clap.Examples;

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
```

Parse the command-line arguments using the `Parser` class.

```csharp
using System;
using Clap.Examples;

var app = ImageConverter.Parse(args);

switch (app.Command) 
{
    case ImageConverterCommands.History history:
        // do something with history
        break;

    case ImageConverterCommands.Publish publish:
        // do something with publish
        break;

    default:
        // do something with app
        break;
}

```

Run the application with command-line arguments.

```bash
dotnet run -- -v "~/Downloads/tree.png" "~/Downloads/tree.jpg" 
dotnet run -- "~/Downloads/tree.png" -e "jpg" publish "https://yourdomain.com/upload"
```

## Features

*   **Strongly-typed:** Define your command-line interface using classes and properties.
*   **Subcommands:** Define subcommands using nested classes.
*   **Options and Arguments:** Define options and arguments using attributes.
*   **Automatic help generation:** Generate help messages automatically.

## Contributing

There is still loads to do, pull-requests are welcome

## License

MIT