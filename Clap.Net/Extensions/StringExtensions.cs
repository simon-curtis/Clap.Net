using System.Text;

namespace Clap.Net.Extensions;

public static class StringExtensions
{
    public static string ToCamelCase(this string name)
    {
        return $"{char.ToLower(name[0])}{name.Substring(1).ToString()}";
    }

    public static string ToSnakeCase(this string name)
    {
        var sb = new StringBuilder($"{char.ToLower(name[0])}");

        foreach (var c in name.Substring(1))
        {
            if (char.IsUpper(c))
            {
                if (sb.Length > 0)
                    sb.Append('_');

                sb.Append(char.ToLower(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}