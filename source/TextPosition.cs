namespace XDLCompiler;

/// <summary>Provides functionality for tracking a position within text.</summary>
public struct TextPosition
{
    private int _offset;

    private int _line;

    private int _column;

    /// <summary>Gets the current offset.</summary>
    public readonly int Offset => _offset;

    /// <summary>Gets the current position's line number.</summary>
    public readonly int Line => _line;

    /// <summary>Gets the current positions' column number.</summary>
    public readonly int Column => _column;

    /// <summary>Initializes a new <see cref="TextPosition"/> instance.</summary>
    public TextPosition(int offset, int line, int column)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(offset, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 0);
        _offset = offset;
        _line = line;
        _column = column;
    }

    /// <summary>Advances the position by the provided text.</summary>
    public void Advance(ReadOnlySpan<char> text)
    {
        _offset += text.Length;
        var lines = text.Count('\n');

        if (lines == 0)
        {
            _column += text.Length;
        }
        else
        {
            _column = text.Length - text.LastIndexOf('\n') - 1;
            _line += lines;
        }
    }

    /// <summary>Advances the position by the provided text.</summary>
    public void Advance(char text)
    {
        _offset++;

        if (text != '\n')
        {
            _column++;
        }
        else
        {
            _line++;
            _column = 1;
        }
    }

    public readonly override string ToString()
    {
        return $"{_line + 1},{_column + 1}";
    }
}
