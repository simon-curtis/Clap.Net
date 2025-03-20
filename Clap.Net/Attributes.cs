// ReSharper disable once CheckNamespace
namespace Clap.Net;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class CommandAttribute : Attribute
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public bool IsDefault { get; init; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class SubcommandAttribute : Attribute
{
    public string? Name { get; init; }
    public string? Description { get; init; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class OptionAttribute : Attribute
{
    public string? LongName { get; init; }
    public char ShortName { get; init; } = '\0';
    public string? Description { get; init; }
    public object? Default { get; init; }
    public bool Required { get; init; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class SwitchAttribute : Attribute
{
    public string? LongName { get; init; }
    public char ShortName { get; init; } = '\0';
    public string? Description { get; init; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class PositionalAttribute : Attribute
{
    public int Index { get; init; }
    public string? Description { get; init; }
    public bool Required { get; init; }
}