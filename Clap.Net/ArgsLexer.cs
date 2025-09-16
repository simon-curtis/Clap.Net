using System;
using System.Collections.Immutable;

namespace Clap.Net;

public static class ArgsLexer
{
    public static IToken[] Lex(ReadOnlySpan<string> args)
    {
        var builder = ImmutableArray.CreateBuilder<IToken>();

        foreach (var arg in args)
        {
            if (arg.Length is 0) continue;

            var span = arg.AsSpan();
            if (span.StartsWith("-".AsSpan()) && span.IndexOf('=') is var equals and > -1)
            {
                var flagPart = LexArg(span.Slice(0, equals));
                builder.Add(flagPart);

                var valuePart = new ValueLiteral(span.Slice(equals + 1).ToString());
                builder.Add(valuePart);
            }
            else
            {
                var token = LexArg(span);
                builder.Add(token);
            }
        }

        return builder.ToArray();
    }

    private static IToken LexArg(ReadOnlySpan<char> arg)
    {
        if (arg.Length is 1 || arg[0] is not '-')
            return new ValueLiteral(arg.ToString());

        if (arg[1] is '-')
        {
            return arg.Slice(0, 5) is "--no-"
                ? new NegatedFlag(new LongFlag(arg.Slice(5).ToString()))
                : new LongFlag(arg.Slice(2).ToString());
        }

        if (arg.Length > 2)
            return new CompoundFlag(arg.Slice(1).ToArray());

        return new ShortFlag(arg[1]);
    }
}

public interface IToken;

public record ShortFlag(char Char) : IToken;

public record CompoundFlag(char[] Chars) : IToken;

public record NegatedFlag(LongFlag Flag) : IToken;

public record LongFlag(string Name) : IToken;

public record ValueLiteral(string Value) : IToken;

public static class TokenExtensions
{
    public static string Format(this IToken token)
    {
        return token switch
        {
            ShortFlag s => $"-{s.Char}",
            CompoundFlag c => $"-{string.Join("", c.Chars)}",
            NegatedFlag n => $"--{n.Flag}",
            ValueLiteral v => v.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(token), token, null)
        };
    }
} 