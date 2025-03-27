// ReSharper disable once CheckNamespace
namespace Clap.Net;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
public sealed class CommandAttribute : Attribute
{
    public string? Name { get; init; }
    public string? Summary { get; init; }
    public string? Description { get; init; }
    public bool IsDefault { get; init; }
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