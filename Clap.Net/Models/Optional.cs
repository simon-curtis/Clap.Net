namespace Clap.Net.Models;

public struct Optional<T>
{
    public T Value { get; private set; }
    public bool HasValue { get; private set; }

    public void SetValue(T value)
    {
        Value = value;
        HasValue = true;
    }

    public static implicit operator Optional<T>(T value) => new()
    {
        HasValue = true,
        Value = value
    };
}