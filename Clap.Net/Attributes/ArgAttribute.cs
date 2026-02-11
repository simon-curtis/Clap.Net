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
    ValueEnumResult FromStr();
}

public class ValueEnumResult
{
    private readonly IValueEnum? _value;
    private readonly Exception? _error;

    private ValueEnumResult(IValueEnum? value, Exception? error)
    {
        _value = value;
        _error = error;
    }

    public bool IsSuccess => _value is not null;
    public bool IsError => _error is not null;

    public IValueEnum Value => _value ?? throw new InvalidOperationException("Result is not Success");
    public Exception Error => _error ?? throw new InvalidOperationException("Result is not Error");

    public static ValueEnumResult Success(IValueEnum value) => new(value, null);
    public static ValueEnumResult Failure(Exception error) => new(null, error);
}

public record PossibleValue(string Name, string? Help = null);