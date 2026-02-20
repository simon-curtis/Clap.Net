namespace Clap.Net.Models;

public record ShowHelp(string HelpMessage);
public record ShowVersion(string Version);
public record ParseError(string Message, string HelpMessage);
