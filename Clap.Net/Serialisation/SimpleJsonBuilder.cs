using System.Collections.Generic;
using System.Text;

namespace Clap.Net.Serialisation;

internal class SimpleJsonBuilder
{
    private const int MaxCachedIndentDepth = 10; // Most CLIs don't nest deeper than 10 levels

    private readonly StringBuilder _sb = new StringBuilder();
    private readonly Stack<bool> _needsComma = new Stack<bool>();
    private readonly bool _prettyPrint;
    private readonly string _indent;
    private readonly string?[] _indentCache = new string?[MaxCachedIndentDepth];
    private int _indentLevel = 0;

    public SimpleJsonBuilder(bool prettyPrint = true, string indent = "  ")
    {
        _prettyPrint = prettyPrint;
        _indent = indent;
    }

    public SimpleJsonBuilder StartObject()
    {
        _sb.Append("{");
        _needsComma.Push(false);
        _indentLevel++;
        return this;
    }

    public SimpleJsonBuilder StartObject(string name)
    {
        AddCommaIfNeeded();
        AddNewLineAndIndent();
        _sb.Append($"\"{EscapeJson(name)}\": {{");
        _needsComma.Push(false);
        _indentLevel++;
        return this;
    }

    public SimpleJsonBuilder EndObject()
    {
        _indentLevel--;
        if (_prettyPrint)
        {
            _sb.AppendLine();
            _sb.Append(GetIndent());
        }

        _sb.Append("}");
        _needsComma.Pop();
        return this;
    }

    public SimpleJsonBuilder StartArray(string name)
    {
        AddCommaIfNeeded();
        AddNewLineAndIndent();
        _sb.Append($"\"{EscapeJson(name)}\": [");
        _needsComma.Push(false);
        _indentLevel++;
        return this;
    }

    public SimpleJsonBuilder StartArray()
    {
        _sb.Append("[");
        _needsComma.Push(false);
        _indentLevel++;
        return this;
    }

    public SimpleJsonBuilder EndArray()
    {
        _indentLevel--;
        if (_prettyPrint)
        {
            _sb.AppendLine();
            _sb.Append(GetIndent());
        }

        _sb.Append("]");
        _needsComma.Pop();
        if (_needsComma.Count > 0)
        {
            _needsComma.Pop();
            _needsComma.Push(true);
        }

        return this;
    }

    public SimpleJsonBuilder AddProperty(string name, string value)
    {
        AddCommaIfNeeded();
        AddNewLineAndIndent();
        _sb.Append($"\"{EscapeJson(name)}\": \"{EscapeJson(value)}\"");
        return this;
    }

    public SimpleJsonBuilder AddProperty(string name, bool value)
    {
        AddCommaIfNeeded();
        AddNewLineAndIndent();
        _sb.Append($"\"{EscapeJson(name)}\": {value.ToString().ToLower()}");
        return this;
    }

    public SimpleJsonBuilder AddProperty(string name, int value)
    {
        AddCommaIfNeeded();
        AddNewLineAndIndent();
        _sb.Append($"\"{EscapeJson(name)}\": {value}");
        return this;
    }

    public SimpleJsonBuilder AddObjectToArray()
    {
        AddCommaIfNeeded();
        AddNewLineAndIndent();
        return StartObject();
    }

    private void AddCommaIfNeeded()
    {
        if (_needsComma.Count > 0 && _needsComma.Peek())
        {
            _sb.Append(",");
        }

        if (_needsComma.Count > 0)
        {
            _needsComma.Pop();
            _needsComma.Push(true);
        }
    }

    private void AddNewLineAndIndent()
    {
        if (_prettyPrint)
        {
            _sb.AppendLine();
            _sb.Append(GetIndent());
        }
    }

    private string GetIndent()
    {
        var spaces = _indentLevel * _indent.Length;

        // Use cache for common depths
        if (_indentLevel < MaxCachedIndentDepth)
        {
            return _indentCache[_indentLevel] ??= new string(' ', spaces);
        }

        // For deeper nesting, create string without caching
        return new string(' ', spaces);
    }

    public override string ToString() => _sb.ToString();

    private string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        // Check if escaping is needed
        var needsEscape = false;
        foreach (var c in value)
        {
            if (c == '\\' || c == '"' || c == '\n' || c == '\r' || c == '\t')
            {
                needsEscape = true;
                break;
            }
        }

        if (!needsEscape)
            return value;

        // Use StringBuilder for single-pass escaping
        var sb = new StringBuilder(value.Length + 4); // Pre-size with small margin
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}