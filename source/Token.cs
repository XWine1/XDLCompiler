namespace XDLCompiler;

/// <summary>A token in an XDL file.</summary>
public readonly struct Token(TokenId id, TextPosition position, int length)
{
    /// <summary>The ID of the token.</summary>
    public TokenId Id { get; } = id;

    /// <summary>The position in the source text of the start of this token.</summary>
    public TextPosition Position { get; } = position;

    /// <summary>The length of this token in the source text.</summary>
    public int Length { get; } = length;

    /// <summary>Gets the textual value of the token.</summary>
    public ReadOnlySpan<char> GetValue(ReadOnlySpan<char> sourceText) => sourceText.Slice(Position.Offset, Length);

    /// <summary>Gets the textual value of the token.</summary>
    public string ToString(ReadOnlySpan<char> sourceText) => GetValue(sourceText).ToString();
}
