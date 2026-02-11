using System.CodeDom.Compiler;
using Clap.Net.Extensions;
using Microsoft.CodeAnalysis;

namespace Clap.Net.Generators;

internal static class ArgumentConversionHelper
{
    public static void WriteArraySetter(
        IndentedTextWriter writer,
        string variableName,
        ITypeSymbol elementType,
        ITypeSymbol? valueParser = null,
        bool isNamed = false)
    {
        var childType = elementType.ToDisplayString(CodeGeneratorConstants.FullNameDisplayFormat);

        if (isNamed)
        {
            // For named arguments: consume only ONE value per flag invocation
            // This allows accumulating multiple values via repeated flags: -t val1 -t val2
            writer.WriteMultiLine(
                $$"""
                  const int MaxArrayElements = 10000;
                  var currentArray = ({{variableName}}.HasValue ? {{variableName}}.Value : null) ?? System.Array.Empty<{{childType}}>();

                  if (currentArray.Length >= MaxArrayElements)
                      throw new System.ArgumentException("Array argument exceeds maximum of " + MaxArrayElements + " elements");

                  var newArray = new {{childType}}[currentArray.Length + 1];
                  currentArray.CopyTo(newArray, 0);
                  newArray[currentArray.Length] = {{GetArgConversion(elementType, "value", valueParser)}};
                  {{variableName}} = newArray;
                  index++;
                  """);
        }
        else
        {
            // For positional arguments: consume all remaining positional values (greedy)
            writer.WriteMultiLine(
                $$"""
                  const int MaxArrayElements = 10000; // Prevent DoS from excessive array arguments
                  var builder = System.Collections.Immutable.ImmutableArray.CreateBuilder<{{childType}}>();
                  var arrayElementCount = 0;
                  while (index < tokens.Length && tokens[index] is Clap.Net.ValueLiteral(var @__clapgen_arrayValue))
                  {
                      if (++arrayElementCount > MaxArrayElements)
                          throw new System.ArgumentException($"Array argument exceeds maximum of {MaxArrayElements} elements");

                      builder.Add({{GetArgConversion(elementType, "@__clapgen_arrayValue", valueParser)}});
                      index++;
                  }
                  {{variableName}} = builder.ToArray();
                  """);
        }
    }

    public static string GetArgConversion(ITypeSymbol member, string variableName, ITypeSymbol? valueParser = null)
    {
        // If a custom parser is provided, use it with error handling
        if (valueParser is not null)
        {
            var parserTypeName = valueParser.ToDisplayString(CodeGeneratorConstants.FullNameDisplayFormat);
            return $"TryParseWithCustomParser({variableName}, {parserTypeName}.Parse)";
        }

        var nullable = member.NullableAnnotation is NullableAnnotation.Annotated;
        var fullName = member.ToDisplayString(CodeGeneratorConstants.FullNameDisplayFormat);
        return member.Name switch
        {
            "String" => variableName,
            "Int32" => nullable
                ? $"int.TryParse({variableName}, out var v) ? v : null"
                : $"TryParseOrThrow<int>({variableName}, int.TryParse, \"integer\")",
            "Int64" => nullable
                ? $"long.TryParse({variableName}, out var v) ? v : null"
                : $"TryParseOrThrow<long>({variableName}, long.TryParse, \"long integer\")",
            "Single" => nullable
                ? $"float.TryParse({variableName}, out var v) ? v : null"
                : $"TryParseOrThrow<float>({variableName}, float.TryParse, \"float\")",
            "Double" => nullable
                ? $"double.TryParse({variableName}, out var v) ? v : null"
                : $"TryParseOrThrow<double>({variableName}, double.TryParse, \"double\")",
            "Decimal" => nullable
                ? $"decimal.TryParse({variableName}, out var v) ? v : null"
                : $"TryParseOrThrow<decimal>({variableName}, decimal.TryParse, \"decimal\")",
            "Boolean" => nullable
                ? $"bool.TryParse({variableName}, out var v) ? v : null"
                : $"TryParseOrThrow<bool>({variableName}, bool.TryParse, \"boolean\")",
            "Byte" => nullable
                ? $"byte.TryParse({variableName}, out var v) ? v : null"
                : $"TryParseOrThrow<byte>({variableName}, byte.TryParse, \"byte\")",
            "SByte" => nullable
                ? $"sbyte.TryParse({variableName}, out var v) ? v : null"
                : $"TryParseOrThrow<sbyte>({variableName}, sbyte.TryParse, \"signed byte\")",
            "Int16" => nullable
                ? $"short.TryParse({variableName}, out var v) ? v : null"
                : $"TryParseOrThrow<short>({variableName}, short.TryParse, \"short integer\")",
            "UInt16" => nullable
                ? $"ushort.TryParse({variableName}, out var v) ? v : null"
                : $"TryParseOrThrow<ushort>({variableName}, ushort.TryParse, \"unsigned short\")",
            "UInt32" => nullable
                ? $"uint.TryParse({variableName}, out var v) ? v : null"
                : $"TryParseOrThrow<uint>({variableName}, uint.TryParse, \"unsigned integer\")",
            "UInt64" => nullable
                ? $"ulong.TryParse({variableName}, out var v) ? v : null"
                : $"TryParseOrThrow<ulong>({variableName}, ulong.TryParse, \"unsigned long\")",
            "Char" => nullable
                ? $"char.TryParse({variableName}, out var v) ? v : null"
                : $"TryParseOrThrow<char>({variableName}, char.TryParse, \"character\")",
            "DateTime" => nullable
                ? $"System.DateTime.TryParse({variableName}, out var v) ? v : null"
                : $"TryParseOrThrow<System.DateTime>({variableName}, System.DateTime.TryParse, \"date/time\")",
            "TimeSpan" => nullable
                ? $"System.TimeSpan.TryParse({variableName}, out var v) ? v : null"
                : $"TryParseOrThrow<System.TimeSpan>({variableName}, System.TimeSpan.TryParse, \"time span\")",
            "Guid" => nullable
                ? $"System.Guid.TryParse({variableName}, out var v) ? v : null"
                : $"TryParseOrThrow<System.Guid>({variableName}, System.Guid.TryParse, \"GUID\")",
            _ => $"({fullName})TryConvertOrThrow({variableName}, typeof({fullName}), \"{fullName}\")"
        };
    }
}
