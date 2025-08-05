using System.Collections.Immutable;

namespace Clap.Net;

public static class Lexer
{
    public static IToken[] Lex(ReadOnlySpan<string> args)
    {
        var builder = ImmutableArray.CreateBuilder<IToken>();

        foreach (var arg in args)
        {
            if (arg.Length is 0) continue;
            var token = LexArg(arg.AsSpan());
            builder.Add(token);
        }

        return builder.ToArray();
    }

    private static IToken LexArg(ReadOnlySpan<char> arg)
    {
        if (arg.Length is 1 || arg[0] is not '-')
        {
            return new ValueLiteral
            {
                Value = arg.ToString()
            };
        }

        if (arg[1] is '-')
        {
            return new LongFlag
            {
                FullFlag = arg.ToString(),
                Name = arg.Slice(2).ToString()
            };
        }

        if (arg.Length > 2)
        {
            return new CompoundFlag
            {
                FullFlag = arg.ToString(),
                Chars = arg.Slice(1).ToArray()
            };
        }

        return new ShortFlag
        {
            FullFlag = arg.ToString(),
            Char = arg[1]
        };
    }
}

public interface IToken;

public readonly struct ShortFlag : IToken
{
    public string FullFlag { get; init; }
    public char Char { get; init; }
}

public readonly struct CompoundFlag : IToken
{
    public string FullFlag { get; init; }
    public char[] Chars { get; init; }
}

public readonly struct NegatedFlag : IToken
{
    public IToken Flag { get; init; }
}

public readonly struct LongFlag : IToken
{
    public string FullFlag { get; init; }
    public string Name { get; init; }
}

public readonly struct ValueLiteral : IToken
{
    public string Value { get; init; }

    public bool TryInt(out int i)
    {
        return int.TryParse(Value, out i);
    }

    public bool TryBool(out bool b)
    {
        return bool.TryParse(Value, out b);
    }
}