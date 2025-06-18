using System.Runtime.CompilerServices;
using OneOf;

namespace Clap.Net.Models;

public record ShowHelp;
public record ShowVersion(string Version);
public record ParseError(string Message);

public class ParseResult<T>(OneOf<T, ShowHelp, ShowVersion, ParseError> _) : OneOfBase<T, ShowHelp, ShowVersion, ParseError>(_)
{
    public ParseResult<TNew> ChangeType<TNew>() where TNew : class
    {
        return this switch
        {
            { IsT0: true, AsT0: var value } => Unsafe.As<TNew>(value),
            { IsT1: true, AsT1: var showHelp } => showHelp,
            { IsT2: true, AsT2: var showVersion } => showVersion,
            { IsT3: true, AsT3: var error } => error,
            _ => throw new Exception("Unable to cast parse result to new type")
        };
    }

    public static implicit operator ParseResult<T>(T _) => new(_);
    public static implicit operator ParseResult<T>(ShowHelp _) => new(_);
    public static implicit operator ParseResult<T>(ShowVersion _) => new(_);
    public static implicit operator ParseResult<T>(ParseError _) => new(_);
}