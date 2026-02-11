using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml.Linq;
using Clap.Net.Extensions;
using Clap.Net.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Clap.Net;

public static class CommandModelParser
{
    private static readonly char[] AllowedCharacters = [' ', '\t', '\n', '\r', '&'];

    internal static CommandModel? GetCommandModel(
        INamedTypeSymbol commandCandidateSymbol,
        TypeDeclarationSyntax? typeDeclarationSyntax)
    {
        var commandAttribute = commandCandidateSymbol
            .GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name is nameof(CommandAttribute));

        if (commandAttribute is null)
            return null;

        var arguments = new List<ArgumentModel>();
        SubCommandArgumentModel? subCommandArgumentModel = null;

        var positionalIndex = 0;
        foreach (var member in commandCandidateSymbol.GetMembers())
        {
            if (member is not IPropertySymbol and not IFieldSymbol
                || member.DeclaredAccessibility is not Accessibility.Public
                || member.IsStatic
                || (member as IFieldSymbol)?.IsConst is true)
                continue;

            var memberType = member switch
            {
                IPropertySymbol property => property.Type,
                IFieldSymbol field => field.Type,
                _ => null,
            };

            if (memberType is null)
                continue;

            var defaultValue = member switch
            {
                IPropertySymbol prop when (PropertyDeclarationSyntax)prop.DeclaringSyntaxReferences[0].GetSyntax() is
                    { Initializer: { } initializer } => initializer.Value.ToString(),
                IFieldSymbol { HasConstantValue: true } field => field.ConstantValue! switch
                {
                    string s => $"\"{s}\"",
                    var other => other.ToString()
                },
                _ => null
            };

            var attributes = member
                .GetAttributes()
                .Where(attr => attr.AttributeClass?.Name is not null)
                .ToDictionary(attr => attr.AttributeClass?.Name!);

            var isRequired = member switch
            {
                IPropertySymbol property => property.IsRequired,
                IFieldSymbol field => field.IsRequired,
                _ => false
            };

            if (attributes.TryGetValue(nameof(CommandAttribute), out _))
            {
                subCommandArgumentModel = new SubCommandArgumentModel(
                    member,
                    memberType,
                    isRequired, defaultValue);
                continue;
            }

            var variableName = GetVariableName(member.Name.ToCamelCase());
            var commentXml = ExtractSummary(member.GetDocumentationCommentXml());

            // Extract ValidationAttributes for properties without [Arg] attribute
            var validationAttributes = member.GetAttributes()
                .Where(attr => attr.AttributeClass is not null &&
                       IsValidationAttribute(attr.AttributeClass))
                .ToImmutableArray();

            if (!attributes.TryGetValue(nameof(ArgAttribute), out var argAttribute))
            {
                // Get the comment from the arg
                arguments.Add(
                    new PositionalArgumentModel(
                        member,
                        memberType,
                        variableName,
                        defaultValue,
                        commentXml,
                        isRequired,
                        positionalIndex++,
                        ValueParser: null,
                        ValidationAttributes: validationAttributes));
                continue;
            }

            var argNamedArguments = argAttribute.NamedArguments.ToDictionary(a => a.Key, a => a.Value.Value);
            var @short = argNamedArguments.GetOrDefault(nameof(ArgAttribute.Short)) as char?;
            var @long = argNamedArguments.GetOrDefault(nameof(ArgAttribute.Long)) as string;
            var argAction = argNamedArguments.GetOrDefault(nameof(ArgAttribute.Action)) switch
            {
                ArgAction action => action,
                int value => (ArgAction)value,
                string value => Enum.TryParse<ArgAction>(value, out var action) ? action : ArgAction.Set,
                _ => ArgAction.Set
            };

            var helpText = argNamedArguments.GetOrDefault(nameof(ArgAttribute.Help)) as string ?? commentXml;

            // Extract ValueParser type if specified
            ITypeSymbol? valueParserType = null;
            if (argNamedArguments.TryGetValue(nameof(ArgAttribute.ValueParser), out var valueParserValue)
                && valueParserValue is INamedTypeSymbol parserSymbol)
            {
                valueParserType = parserSymbol;
            }

            ArgumentModel argument = @short is not null || @long is not null
                ? new NamedArgumentModel(
                    member,
                    memberType,
                    variableName,
                    defaultValue,
                    Help: helpText,
                    Short: @short,
                    Long: @long,
                    Env: argNamedArguments.GetOrDefault(nameof(ArgAttribute.Env)) as string,
                    Negation: argNamedArguments.GetOrDefault(nameof(ArgAttribute.Negation)) as bool?,
                    argAction,
                    isRequired,
                    ValueParser: valueParserType,
                    ValidationAttributes: validationAttributes)
                : new PositionalArgumentModel(
                    member, memberType, variableName, defaultValue, helpText, isRequired, positionalIndex++, valueParserType,
                    ValidationAttributes: validationAttributes);

            arguments.Add(argument);
        }

        var args = commandAttribute.NamedArguments
            .ToDictionary(a => a.Key, a => a.Value.Value);

        var commentary = typeDeclarationSyntax is not null
            ? ExtractSummary(typeDeclarationSyntax)
            : null;

        return new CommandModel(
            Name: args.GetOrDefault(nameof(CommandAttribute.Name)) as string ??
                  commandCandidateSymbol.Name.ToSnakeCase(),
            About: args.GetOrDefault(nameof(CommandAttribute.About)) as string ?? commentary?.About,
            LongAbout: args.GetOrDefault(nameof(CommandAttribute.LongAbout)) as string ?? commentary?.LongAbout,
            Version: args.GetOrDefault(nameof(CommandAttribute.Version)) as string,
            Symbol: commandCandidateSymbol,
            SubCommandArgumentModel: subCommandArgumentModel,
            Arguments: [.. arguments],
            IsCliCommand: commandCandidateSymbol
                .GetAttributes()
                .Any(attr => attr.AttributeClass?.Name is nameof(CliAttribute)),
            IsSubCommand: commandCandidateSymbol.BaseType?
                .GetAttributes()
                .Any(attr => attr.AttributeClass?.Name is nameof(SubCommandAttribute)) is true);
    }

    private static string GetVariableName(string name) => name switch
    {
        "params" => "@__clapgen_params",
        "base" => "@__clapgen_base",
        "this" => "@__clapgen_this",
        "default" => "@__clapgen_default",
        "event" => "@__clapgen_event",
        "field" => "@__clapgen_field",
        "var" => "@__clapgen_var",
        _ => $"@__clapgen_{name}"
    };


    private static (string About, string? LongAbout)? ExtractSummary(TypeDeclarationSyntax typeDecl)
    {
        // Get the XML doc trivia attached to the type
        var trivia = typeDecl.GetLeadingTrivia()
            .Select(t => t.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .FirstOrDefault();

        if (trivia is null)
            return null;

        // Find the <summary> element
        var summaryElement = trivia.Content
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(e => e.StartTag.Name.LocalName.Text == "summary");

        if (summaryElement is null)
            return null;

        // Extract the text inside <summary>
        var lines = summaryElement.Content
            .OfType<XmlTextSyntax>()
            .SelectMany(x => x.TextTokens)
            .Select(t => t.Text.Trim())
            .Where(t => t.Length > 0)
            .ToList();

        if (lines.Count == 0)
            return null;

        return (
            About: lines[0],
            LongAbout: lines.Count == 1 ? null : string.Join("\n", lines)
        );
    }

    private static string? ExtractSummary(string? xmlDocs)
    {
        if (string.IsNullOrWhiteSpace(xmlDocs))
            return null;

        try
        {
            var doc = XDocument.Parse(xmlDocs);
            return doc.Root?.Element("summary")?.Value.Trim();
        }
        catch (System.Xml.XmlException)
        {
            // Malformed XML in documentation comments - skip documentation gracefully
            return null;
        }
    }

    // private static (string About, string? LongAbout)? ExtractSummary(ReadOnlySpan<char> source)
    // {
    //     const string summaryStartTag = "<summary>";
    //     const string summaryEndTag = "</summary>";
    //
    //     var start = source.IndexOf(summaryStartTag.AsSpan());
    //     var end = source.IndexOf(summaryEndTag.AsSpan());
    //
    //     if (start == -1 || end == -1 || end < start)
    //         return null;
    //
    //     // Slice between <summary> and </summary>
    //     start += summaryStartTag.Length;
    //     var contentSpan = source.Slice(start, end - start);
    //
    //     var lines = new List<string>();
    //     while (!contentSpan.IsEmpty)
    //     {
    //         var newLineIndex = contentSpan.IndexOf('\n');
    //         ReadOnlySpan<char> line;
    //
    //         if (newLineIndex >= 0)
    //         {
    //             line = contentSpan.Slice(0, newLineIndex);
    //             contentSpan = contentSpan.Slice(newLineIndex + 1);
    //         }
    //         else
    //         {
    //             line = contentSpan;
    //             contentSpan = ReadOnlySpan<char>.Empty;
    //         }
    //
    //         if (line.StartsWith("///".AsSpan()))
    //             line = line.Slice(3);
    //
    //         var trimmed = RemoveInvalidCharacters(line).Trim();
    //         if (trimmed.Length > 0)
    //             lines.Add(trimmed);
    //     }
    //
    //     return (
    //         About: lines[0],
    //         LongAbout: lines.Count is 1 ? null : string.Join("\n", lines)
    //     );
    // }

    private static string RemoveInvalidCharacters(ReadOnlySpan<char> source)
    {
        Span<char> span = stackalloc char[source.Length];
        var index = 0;
        var spanIndex = 0;
        while (index < source.Length)
        {
            var c = source[index];
            if (char.IsLetterOrDigit(c) || AllowedCharacters.Contains(c))
                span[spanIndex++] = c;

            index++;
        }

        return span.Slice(0, spanIndex).ToString();
    }

    private static bool IsValidationAttribute(INamedTypeSymbol typeSymbol)
    {
        // Check if this type or any of its base types is ValidationAttribute
        var current = typeSymbol;
        while (current is not null)
        {
            if (current.ToString() == "System.ComponentModel.DataAnnotations.ValidationAttribute")
                return true;

            current = current.BaseType;
        }

        return false;
    }
}