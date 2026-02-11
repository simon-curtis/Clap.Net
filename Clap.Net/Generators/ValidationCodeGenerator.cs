using System.CodeDom.Compiler;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Clap.Net.Models;
using Microsoft.CodeAnalysis;

namespace Clap.Net.Generators;

internal static class ValidationCodeGenerator
{
    public static void GenerateValidationCode(
        IndentedTextWriter writer,
        ArgumentModel argument,
        string parsedValueVariable)
    {
        var validationAttrs = argument switch
        {
            NamedArgumentModel named => named.ValidationAttributes,
            PositionalArgumentModel positional => positional.ValidationAttributes,
            _ => ImmutableArray<AttributeData>.Empty
        };

        if (validationAttrs.IsDefaultOrEmpty)
            return;

        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine($"var validationResults = new System.Collections.Generic.List<System.ComponentModel.DataAnnotations.ValidationResult>();");
        writer.WriteLine($"var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext({parsedValueVariable}) {{ MemberName = \"{argument.Symbol.Name}\" }};");
        writer.WriteLine($"var validationAttributes = new System.ComponentModel.DataAnnotations.ValidationAttribute[]");
        writer.WriteLine("{");
        writer.Indent++;

        foreach (var attr in validationAttrs)
        {
            var attrType = attr.AttributeClass!.ToDisplayString(CodeGeneratorConstants.FullNameDisplayFormat);
            var attrConstruction = GenerateAttributeConstruction(attr, attrType);
            writer.WriteLine($"{attrConstruction},");
        }

        writer.Indent--;
        writer.WriteLine("};");
        writer.WriteLine();
        writer.WriteLine($"if (!System.ComponentModel.DataAnnotations.Validator.TryValidateValue({parsedValueVariable}, validationContext, validationResults, validationAttributes))");
        writer.WriteLine("{");
        writer.Indent++;
        writer.WriteLine("var errors = string.Join(\"; \", validationResults.Select(r => r.ErrorMessage));");
        writer.WriteLine($"return new Clap.Net.Models.ParseError($\"Validation failed for '{argument.Symbol.Name}': {{errors}}\", GetFormattedHelpMessage());");
        writer.Indent--;
        writer.WriteLine("}");
        writer.Indent--;
        writer.WriteLine("}");
    }

    private static string GenerateAttributeConstruction(AttributeData attr, string attrType)
    {
        var sb = new StringBuilder();
        sb.Append($"new {attrType}(");

        // Constructor arguments
        if (attr.ConstructorArguments.Length > 0)
        {
            var args = attr.ConstructorArguments.Select(arg => FormatTypedConstant(arg));
            sb.Append(string.Join(", ", args));
        }

        sb.Append(")");

        // Named arguments (properties)
        if (attr.NamedArguments.Length > 0)
        {
            sb.Append(" { ");
            var namedArgs = attr.NamedArguments.Select(kvp =>
                $"{kvp.Key} = {FormatTypedConstant(kvp.Value)}");
            sb.Append(string.Join(", ", namedArgs));
            sb.Append(" }");
        }

        return sb.ToString();
    }

    private static string FormatTypedConstant(TypedConstant constant)
    {
        if (constant.IsNull)
            return "null";

        return constant.Kind switch
        {
            TypedConstantKind.Primitive => constant.Type?.SpecialType switch
            {
                SpecialType.System_String => EscapeStringLiteral(constant.Value?.ToString() ?? string.Empty),
                SpecialType.System_Char => EscapeCharLiteral(constant.Value?.ToString() ?? string.Empty),
                SpecialType.System_Boolean => constant.Value?.ToString()?.ToLowerInvariant() ?? "false",
                _ => constant.Value?.ToString() ?? "null"
            },
            TypedConstantKind.Enum => $"({constant.Type?.ToDisplayString(CodeGeneratorConstants.FullNameDisplayFormat)}){constant.Value}",
            TypedConstantKind.Type => constant.Value is ITypeSymbol typeSymbol
                ? $"typeof({typeSymbol.ToDisplayString(CodeGeneratorConstants.FullNameDisplayFormat)})"
                : "null",
            TypedConstantKind.Array => $"new[] {{ {string.Join(", ", constant.Values.Select(FormatTypedConstant))} }}",
            _ => constant.Value?.ToString() ?? "null"
        };
    }

    /// <summary>
    /// Escapes a string value for safe inclusion in generated C# code.
    /// Uses verbatim string literals with proper quote escaping.
    /// </summary>
    private static string EscapeStringLiteral(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "@\"\"";

        // Use verbatim string literal and escape quotes by doubling them
        var escaped = value.Replace("\"", "\"\"");
        return $"@\"{escaped}\"";
    }

    /// <summary>
    /// Escapes a character value for safe inclusion in generated C# code.
    /// </summary>
    private static string EscapeCharLiteral(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 1)
            return "'\\0'";

        var c = value[0];
        return c switch
        {
            '\'' => "'\\''",
            '\\' => @"'\\'",
            '\0' => "'\\0'",
            '\a' => "'\\a'",
            '\b' => "'\\b'",
            '\f' => "'\\f'",
            '\n' => "'\\n'",
            '\r' => "'\\r'",
            '\t' => "'\\t'",
            '\v' => "'\\v'",
            _ => $"'{c}'"
        };
    }
}
