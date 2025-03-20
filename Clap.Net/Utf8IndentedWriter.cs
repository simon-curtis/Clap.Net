using System.Text;

namespace Clap.Net;

public ref struct Utf8IndentedWriter(MemoryStream stream, string indentString = "    ")
{
    private readonly Encoding _encoding = Encoding.UTF8;
    private int _indentLevel = 0;
    private bool _atLineStart = true;
    private byte[] _scratchBuffer = new byte[512]; // Reusable buffer to avoid allocs

    public void IncreaseIndent(int by = 1) => _indentLevel += by;
    public void DecreaseIndent(int by = 1) => _indentLevel = Math.Max(0, _indentLevel - by);

    public void WriteLine(string text)
    {
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                int length = i - start;
                if (length > 0 && text[i - 1] == '\r') // Handle CRLF
                    length--;

                WriteLineSegment(text, start, length);
                start = i + 1;
            }
        }

        // Write remaining part (even empty lines)
        if (start <= text.Length - 1)
        {
            int length = text.Length - start;
            if (length > 0 && text.Last() == '\r')
                length--;

            WriteLineSegment(text, start, length);
        }
    }

    public void WriteLine(char c)
    {
        if (_atLineStart)
            WriteIndent();

        WriteUtf8(c.ToString());
        WriteUtf8("\n");
        _atLineStart = true;
    }

    public void WriteLine()
    {
        WriteUtf8("\n");
        _atLineStart = true;
    }

    public void Write(string text)
    {
        if (_atLineStart)
            WriteIndent();

        WriteUtf8(text);
        _atLineStart = false;
    }

    private void WriteLineSegment(string text, int start, int length)
    {
        if (_atLineStart)
            WriteIndent();

        WriteUtf8(text.Substring(start, length));
        WriteUtf8("\n");
        _atLineStart = true;
    }

    private void WriteIndent()
    {
        for (int i = 0; i < _indentLevel; i++)
            WriteUtf8(indentString);
        _atLineStart = false;
    }

    private void WriteUtf8(string text)
    {
        int byteCount = _encoding.GetByteCount(text);
        if (byteCount > _scratchBuffer.Length)
        {
            _scratchBuffer = new byte[Math.Max(byteCount, _scratchBuffer.Length * 2)];
        }

        _encoding.GetBytes(text, 0, text.Length, _scratchBuffer, 0);
        stream.Write(_scratchBuffer, 0, byteCount);
    }
}