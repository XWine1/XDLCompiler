using System.Diagnostics;
using System.Collections.Frozen;

namespace XDLCompiler;

/// <summary>Provides a tokenizer implementation for XDL source code.</summary>
public ref struct TokenReader
{
    private TextPosition _position;

    private readonly ReadOnlySpan<char> _text;

    /// <summary>Gets the current lexer state.</summary>
    public readonly TokenReaderState State => new TokenReaderState(_position);

    /// <summary>Initializes a new <see cref="TokenReader"/> instance.</summary>
    public TokenReader(ReadOnlySpan<char> text)
        : this(text, new TokenReaderState())
    {
    }

    /// <summary>Initializes a new <see cref="TokenReader"/> instance.</summary>
    public TokenReader(ReadOnlySpan<char> text, TokenReaderState state)
    {
        _text = text;
        _position = state.Position;
    }

    /// <summary>Gets the next token.</summary>
    public bool GetNextToken(out Token token)
    {
        token = default;
        SkipWhiteSpaceAndComments();

        if (_position.Offset >= _text.Length)
            return false;

        var start = _position;

        if (_text[start.Offset] is '"' or '\'')
            token = GetLiteralToken();
        else if (char.IsDigit(_text[start.Offset]))
            token = GetNumberToken();
        else if (char.IsLetter(_text[start.Offset]) || _text[start.Offset] == '_')
            token = GetIdentifierToken();
        else if (s_Tokens.TryGetValue(_text[start.Offset], out var id))
            token = new(id, start, Advance(1) - start.Offset);
        else
            token = new Token(TokenId.Unknown, start, Advance(1) - start.Offset);
        
        return true;
    }

    /// <summary>Skips any whitespace and comments at the current position in the text.</summary>
    private int SkipWhiteSpaceAndComments()
    {
        int start = _position.Offset;

        while (_position.Offset < _text.Length)
        {
            int offset = _position.Offset;

            if (char.IsWhiteSpace(_text[_position.Offset]))
                offset++;
            else if (_text[_position.Offset..].StartsWith("//"))
                offset = _text.IndexAfter('\n', _position.Offset);
            else if (_text[_position.Offset..].StartsWith("/*"))
                offset = _text.IndexAfter("*/", _position.Offset);
            else
                break;

            if (offset == -1)
                offset = _text.Length;

            SetOffset(offset);
        }

        return _position.Offset - start;
    }

    /// <summary>Sets the text position.</summary>
    private void SetOffset(int offset)
    {
        if (offset >= _position.Offset)
        {
            Advance(offset - _position.Offset);
            return;
        }

        _position = new TextPosition();
        _position.Advance(_text[..offset]);
    }

    /// <summary>Advances the text position.</summary>
    private int Advance(int count)
    {
        _position.Advance(_text.Slice(_position.Offset, count));
        return _position.Offset;
    }

    /// <summary>Parses a string or character literal token.</summary>
    private Token GetLiteralToken()
    {
        var start = _position;
        var quote = _text[start.Offset];
        Debug.Assert(quote is '"' or '\'');

        // Truncate the input text at the first newline character to handle
        // strings that don't have their terminating quote on the same line.
        Advance(1);
        SetOffset(_text.TruncateAt('\n', _position.Offset).IndexAfter(quote, '\\', _position.Offset));

        if (_position.Offset == -1)
        {
            SetOffset(start.Offset);
            throw new InvalidDataException("Literal has missing terminator.");
        }

        return new(quote == '"' ? TokenId.StringLiteral : TokenId.CharacterLiteral, start, _position.Offset - start.Offset);
    }

    /// <summary>Parses a number token.</summary>
    private Token GetNumberToken()
    {
        var start = _position;
        bool isHex = false;
        bool hasDecimalPoint = false;

        if (_text[_position.Offset..].StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            isHex = true;
            Advance("0x".Length);
        }

        while (_position.Offset < _text.Length)
        {
            char c = _text[_position.Offset];

            if (isHex)
            {
                if (!IsHexDigit(c))
                {
                    break;
                }
            }
            else
            {
                if (c == '.' && !hasDecimalPoint)
                {
                    hasDecimalPoint = true;
                }
                else if (!char.IsDigit(c))
                {
                    break;
                }
            }

            Advance(1);
        }

        return new(TokenId.Number, start, _position.Offset - start.Offset);
    }

    /// <summary>Parses an identifier or keyword token.</summary>
    private Token GetIdentifierToken()
    {
        var start = _position;

        while (_position.Offset < _text.Length && IsIdentifierChar(_text[_position.Offset]))
            Advance(1);

        var value = _text[start.Offset.._position.Offset];
        return new Token(GetIdentifierId(value), start, _position.Offset - start.Offset);
    }

    /// <summary>Indicates whether the provided character is valid in an identifier.</summary>
    private static bool IsIdentifierChar(char c)
    {
        return c == '_' || char.IsLetterOrDigit(c);
    }

    /// <summary>Indicates whether the provided character is valid in a hexadecimal number.</summary>
    private static bool IsHexDigit(char c)
    {
        return char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }

    /// <summary>Gets an appropriate <see cref="TokenId"/> for the provided identifier value</summary>
    private static TokenId GetIdentifierId(ReadOnlySpan<char> value)
    {
        if (s_KeywordLookup.TryGetValue(value, out TokenId id))
            return id;

        return TokenId.Identifier;
    }

    /// <summary>Maps single characters to their <see cref="TokenId"/> values.</summary>
    private static readonly FrozenDictionary<char, TokenId> s_Tokens = new Dictionary<char, TokenId>
    {
        { '{', TokenId.OpeningBrace },
        { '}', TokenId.ClosingBrace },
        { '[', TokenId.OpeningBracket },
        { ']', TokenId.ClosingBracket },
        { '(', TokenId.OpeningParenthesis },
        { ')', TokenId.ClosingParenthesis },
        { ';', TokenId.Semicolon },
        { ':', TokenId.Colon },
        { '.', TokenId.Period },
        { ',', TokenId.Comma },
        { '*', TokenId.Asterisk },
        { '&', TokenId.Ampersand },
        { '+', TokenId.Plus },
        { '-', TokenId.Minus },
        { '=', TokenId.Equals },
    }.ToFrozenDictionary();

    /// <summary>Maps keyword strings to their <see cref="TokenId"/> values.</summary>
    private static readonly FrozenDictionary<string, TokenId> s_Keywords = new Dictionary<string, TokenId>()
    {
        { "enum", TokenId.Enum },
        { "struct", TokenId.Struct },
        { "class", TokenId.Class },
        { "union", TokenId.Union },
        { "interface", TokenId.Interface },
        { "namespace", TokenId.Namespace },
        { "const", TokenId.Const },
        { "import", TokenId.Import },
    }.ToFrozenDictionary();

    /// <summary>Provides access to <see cref="s_Keywords"/> using <see cref="ReadOnlySpan{T}"/> keys.</summary>
    private static readonly FrozenDictionary<string, TokenId>.AlternateLookup<ReadOnlySpan<char>> s_KeywordLookup = s_Keywords.GetAlternateLookup<ReadOnlySpan<char>>();
}
