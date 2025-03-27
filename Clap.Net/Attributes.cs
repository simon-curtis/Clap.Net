// ReSharper disable once CheckNamespace
namespace Clap.Net;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
public sealed class CommandAttribute : Attribute
{
    /// <summary>
    /// A custom name for the app. Will default to the name of the class or struct.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// A custom version for the app. Will default to the version specified in the csproj file <Version>2.0.0</Version>
    /// </summary>
    public string? Version { get; init; }
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public bool SubCommand { get; init; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class SubCommandAttribute : Attribute
{
    public string? Name { get; init; }
    public string? Description { get; init; }
}

public sealed class ArgsAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ArgAttribute : Attribute
{
    public char ShortName { get; init; } = '\0';
    public string? LongName { get; init; }
    public int Index { get; init; }
    public string? Description { get; init; }
    public bool Required { get; init; }
    public bool Last { get; init; }
    public string? Env { get; init; }
}