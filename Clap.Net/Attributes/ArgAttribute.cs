using System;

// ReSharper disable once CheckNamespace
namespace Clap.Net;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ArgAttribute : Attribute
{
    public string? Id { get; init; }

    public Type? ValueParser { get; init; }

    public ArgAction Action { get; init; } = ArgAction.Set;

    public string? Help { get; init; }

    public string? LongHelp { get; init; }

    public char Short { get; init; } = '\0';

    public string? Long { get; init; }

    public string? Env { get; init; }
    public bool Negation { get; init; }

    public bool FromGlobal { get; init; }

    public IValueEnum? ValueEnum { get; init; }

    public bool Skip { get; init; }
}

public enum ArgAction
{
    Set,
    Append,
    SetTrue,
    SetFalse,
    Count,
    Help,
    Version,
}

public interface IValueEnum
{
    IValueEnum[] ValueVariants();
    PossibleValue? ToPossibleValue();
    OneOf.OneOf<IValueEnum, Exception> FromStr();
}

public record PossibleValue(string Name, string? Help = null);