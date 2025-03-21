using System.Runtime.InteropServices;
using Clap.TestConsole.Examples;

System.IO.Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]);
if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)){
    
}

var app = Git.Parse(args);
Console.WriteLine($"Verbose: {app.Verbose}");

switch (app.Command)
{
    case Commands.StatusCommand:
        Console.WriteLine("Status");
        break;

    case Commands.Add add:
        Console.WriteLine($"Adding {string.Join(", ", add.Paths)}");
        break;

    case Commands.Diff diff:
        Console.WriteLine($"Diffing {diff.Base} {diff.Head} {diff.Path}");
        break;

    case Commands.Clone clone:
        Console.WriteLine($"Cloning: {clone.Repository} {clone.Directory}");
        break;

    default:
        Console.WriteLine("Unknown command");
        break;
}