namespace Clap.Net.Models;

public readonly struct FieldValue<T>
{
    public FieldValue()
    {
        Value = default!;
        HasValue = false;
    }

    public T Value { get; private init; } = default!;
    public bool HasValue { get; private init; }

    public static implicit operator FieldValue<T>(T value) => new()
    {
        HasValue = true,
        Value = value
    };
}