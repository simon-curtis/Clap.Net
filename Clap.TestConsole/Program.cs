using Clap.TestConsole;

var app = Git.Parse(args);
Console.WriteLine($"Verbose: {app.Verbose}");

switch (app.Command)
{
    case Commands.Add add:
        Console.WriteLine($"Adding {add.Path}");
        break;

    case Commands.Status:
        Console.WriteLine("Status");
        break;

    case Commands.Diff diff:
        Console.WriteLine($"Diffing {diff.Base} {diff.Head} {diff.Path}");
        break;

    default:
        Console.WriteLine("Unknown command");
        break;
}