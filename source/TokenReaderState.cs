namespace XDLCompiler;

/// <summary>Stores the state of a <see cref="TokenReader"/>.</summary>
public readonly struct TokenReaderState(TextPosition position)
{
    /// <summary>The line and column information.</summary>
    public TextPosition Position { get; } = position;
}
