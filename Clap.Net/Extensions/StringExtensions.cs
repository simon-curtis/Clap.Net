using System;
using System.Text;

namespace Clap.Net.Extensions;

public static class StringExtensions
{
    public static string ToCamelCase(this string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        if (name.Length == 1)
            return char.IsLower(name[0]) ? name : char.ToLower(name[0]).ToString();

        // If already camelCase, return as-is to avoid allocation
        if (char.IsLower(name[0]))
            return name;

        // Build string without allocating substring
        var sb = new StringBuilder(name.Length);
        sb.Append(char.ToLower(name[0]));
        for (int i = 1; i < name.Length; i++)
            sb.Append(name[i]);

        return sb.ToString();
    }

    public static string ToSnakeCase(this string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Count uppercase letters to determine final size
        var upperCount = 0;
        for (var i = 1; i < name.Length; i++)
            if (char.IsUpper(name[i])) upperCount++;

        // If no uppercase letters after first char, just lowercase the first char if needed
        if (upperCount == 0)
        {
            if (char.IsLower(name[0]))
                return name;

            var sb = new StringBuilder(name.Length);
            sb.Append(char.ToLower(name[0]));
            for (int i = 1; i < name.Length; i++)
                sb.Append(name[i]);
            return sb.ToString();
        }

        // Pre-size StringBuilder for better performance
        var sbFull = new StringBuilder(name.Length + upperCount);
        sbFull.Append(char.ToLower(name[0]));

        // Use indexer instead of Substring to avoid string allocation
        for (int i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                sbFull.Append('_');
                sbFull.Append(char.ToLower(c));
            }
            else
            {
                sbFull.Append(c);
            }
        }

        return sbFull.ToString();
    }
}