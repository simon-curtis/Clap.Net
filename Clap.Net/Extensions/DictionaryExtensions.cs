using System.Collections.Generic;

namespace Clap.Net.Extensions;

public static class DictionaryExtensions
{
    public static T? GetOrDefault<TKey, T>(this Dictionary<TKey, T> source, TKey key)
    {
        return source.TryGetValue(key, out var value) ? value : default;
    }
}