using System;
using System.Collections.Immutable;

namespace Clap.Net;

/// <summary>
/// Lexical analyzer for command-line arguments.
/// Tokenizes raw argument strings into structured tokens (flags, options, values).
/// </summary>
public static class ArgsLexer
{
    // Maximum length for a single argument to prevent DoS attacks
    private const int MaxArgumentLength = 32768; // 32KB

    /// <summary>
    /// Tokenizes command-line arguments into a structured token array.
    /// </summary>
    /// <param name="args">The raw command-line arguments to tokenize.</param>
    /// <returns>An array of tokens representing flags, options, and values.</returns>
    /// <exception cref="ArgumentException">Thrown when an argument exceeds the maximum length of 32KB.</exception>
    /// <remarks>
    /// <para>Supports the following token types:</para>
    /// <list type="bullet">
    /// <item><description>Short flags: -v, -f</description></item>
    /// <item><description>Long flags: --verbose, --help</description></item>
    /// <item><description>Negated flags: --no-color</description></item>
    /// <item><description>Compound flags: -vvv, -abc</description></item>
    /// <item><description>Options with values: --output=file.txt, -o=file.txt</description></item>
    /// <item><description>Value literals: file.txt, input.json</description></item>
    /// </list>
    /// <para>Null or empty arguments are skipped during tokenization.</para>
    /// </remarks>
    public static IToken[] Lex(ReadOnlySpan<string> args)
    {
        var builder = ImmutableArray.CreateBuilder<IToken>();

        foreach (var arg in args)
        {
            // Validate argument is not null or empty
            if (string.IsNullOrEmpty(arg))
                continue;

            // Protect against DoS attacks with excessively long arguments
            if (arg.Length > MaxArgumentLength)
            {
                throw new ArgumentException(
                    $"Argument exceeds maximum length of {MaxArgumentLength} characters: '{arg.Substring(0, Math.Min(50, arg.Length))}...'");
            }

            var span = arg.AsSpan();
            // Ensure equals sign is at position 2 or later (e.g., "-a=value" or "--flag=value")
            // This prevents malformed arguments like "=-value" or "-=value"
            if (span.StartsWith("-".AsSpan()) && span.IndexOf('=') is var equals and > 1)
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
            return arg.Length >= 5 && arg.Slice(0, 5).SequenceEqual("--no-".AsSpan())
                ? new NegatedFlag(new LongFlag(arg.Slice(5).ToString()))
                : new LongFlag(arg.Slice(2).ToString());
        }

        if (arg.Length > 2)
            return new CompoundFlag(arg.Slice(1).ToArray());

        return new ShortFlag(arg[1]);
    }
}

/// <summary>
/// Base interface for all command-line argument tokens.
/// </summary>
public interface IToken;

/// <summary>
/// Represents a short flag (single character preceded by a single dash).
/// Example: -v, -f, -h
/// </summary>
/// <param name="Char">The flag character (without the dash).</param>
public record ShortFlag(char Char) : IToken;

/// <summary>
/// Represents multiple short flags combined into one argument.
/// Example: -abc is equivalent to -a -b -c
/// </summary>
/// <param name="Chars">The array of flag characters.</param>
public record CompoundFlag(char[] Chars) : IToken;

/// <summary>
/// Represents a negated long flag (preceded by --no-).
/// Example: --no-color, --no-verify
/// </summary>
/// <param name="Flag">The underlying long flag being negated.</param>
public record NegatedFlag(LongFlag Flag) : IToken;

/// <summary>
/// Represents a long flag (preceded by double dash).
/// Example: --verbose, --help, --output
/// </summary>
/// <param name="Name">The flag name (without the dashes).</param>
public record LongFlag(string Name) : IToken;

/// <summary>
/// Represents a value literal (positional argument or option value).
/// Example: file.txt, input.json, 123
/// </summary>
/// <param name="Value">The literal value string.</param>
public record ValueLiteral(string Value) : IToken;

/// <summary>
/// Extension methods for token formatting and manipulation.
/// </summary>
public static class TokenExtensions
{
    /// <summary>
    /// Formats a token back to its command-line string representation.
    /// </summary>
    /// <param name="token">The token to format.</param>
    /// <returns>The string representation of the token as it would appear on the command line.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the token type is unknown.</exception>
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