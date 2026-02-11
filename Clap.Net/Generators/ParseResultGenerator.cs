using System.CodeDom.Compiler;
using Clap.Net.Extensions;

namespace Clap.Net.Generators;

internal static class ParseResultGenerator
{
    /// <summary>
    /// Generates a custom ParseResult class for a command type.
    /// </summary>
    /// <param name="writer">The writer to output code to</param>
    /// <param name="fullTypeName">The fully qualified type name (e.g., "Clap.Net.Tests.ShortFlagApp")</param>
    /// <param name="resultClassName">The name for the ParseResult class (e.g., "ShortFlagAppParseResult")</param>
    public static void GenerateParseResultClass(
        IndentedTextWriter writer,
        string fullTypeName,
        string resultClassName)
    {
        writer.WriteMultiLine(
            $$"""
              public class {{resultClassName}}
              {
                  private enum ResultType { Success, Help, Version, Error }

                  private readonly ResultType _type;
                  private readonly {{fullTypeName}}? _command;
                  private readonly Clap.Net.Models.ShowHelp? _help;
                  private readonly Clap.Net.Models.ShowVersion? _version;
                  private readonly Clap.Net.Models.ParseError? _error;

                  private {{resultClassName}}({{fullTypeName}} command)
                  {
                      _type = ResultType.Success;
                      _command = command;
                  }

                  private {{resultClassName}}(Clap.Net.Models.ShowHelp help)
                  {
                      _type = ResultType.Help;
                      _help = help;
                  }

                  private {{resultClassName}}(Clap.Net.Models.ShowVersion version)
                  {
                      _type = ResultType.Version;
                      _version = version;
                  }

                  private {{resultClassName}}(Clap.Net.Models.ParseError error)
                  {
                      _type = ResultType.Error;
                      _error = error;
                  }

                  public bool IsSuccess => _type == ResultType.Success;
                  public bool IsHelp => _type == ResultType.Help;
                  public bool IsVersion => _type == ResultType.Version;
                  public bool IsError => _type == ResultType.Error;

                  public {{fullTypeName}} Command => _command ?? throw new System.InvalidOperationException("Result is not Success");
                  public Clap.Net.Models.ShowHelp Help => _help ?? throw new System.InvalidOperationException("Result is not Help");
                  public Clap.Net.Models.ShowVersion Version => _version ?? throw new System.InvalidOperationException("Result is not Version");
                  public Clap.Net.Models.ParseError Error => _error ?? throw new System.InvalidOperationException("Result is not Error");

                  public TNewParseResult ChangeType<TNewParseResult>() where TNewParseResult : class
                  {
                      object? value = _type switch
                      {
                          ResultType.Success => _command,
                          ResultType.Help => _help,
                          ResultType.Version => _version,
                          ResultType.Error => _error,
                          _ => throw new System.InvalidOperationException("Unknown result type")
                      };

                      if (value is TNewParseResult result)
                          return result;

                      throw new System.InvalidCastException(
                          $"Cannot cast {value?.GetType().FullName ?? "null"} to {typeof(TNewParseResult).FullName}");
                  }

                  public static implicit operator {{resultClassName}}({{fullTypeName}} value) => new(value);
                  public static implicit operator {{resultClassName}}(Clap.Net.Models.ShowHelp value) => new(value);
                  public static implicit operator {{resultClassName}}(Clap.Net.Models.ShowVersion value) => new(value);
                  public static implicit operator {{resultClassName}}(Clap.Net.Models.ParseError value) => new(value);
              }
              """);
    }

    /// <summary>
    /// Creates a ParseResult class name from a full type name.
    /// </summary>
    /// <param name="fullTypeName">The fully qualified type name</param>
    /// <returns>The result class name with generic brackets replaced</returns>
    public static string CreateResultClassName(string fullTypeName)
    {
        var simpleTypeName = fullTypeName.Substring(fullTypeName.LastIndexOf('.') + 1);
        return $"{simpleTypeName.Replace('<', '_').Replace('>', '_')}ParseResult";
    }
}
