using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace XDLCompiler;

/// <summary>Provides parsing functionality for XDL source code.</summary>
public sealed class XdlReader
{
    private readonly string _text;

    private TokenReaderState _state;

    private int _namespaceDepth;

    private readonly Stack<string> _namespaces = [];

    /// <summary>Initializes a new <see cref="XdlReader"/> instance.</summary>
    public XdlReader(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
        _state = new TokenReaderState();
    }

    /// <summary>Parses the next object from the file.</summary>
    public bool TryReadNext([NotNullWhen(true)] out SyntaxNode? node)
    {
    TryAgain:
        var attributePosition = _state.Position;
        bool hasAttributes = TryReadAttributes(out var attributes);

        if (!TryPeekToken(out Token token))
        {
            node = null;
            return false;
        }

        if (token.Id == TokenId.ClosingBrace && _namespaceDepth > 0)
        {
            _namespaceDepth--;
            _namespaces.Pop();

            if (hasAttributes)
                Unexpected("Attribute", attributePosition);

            ReadToken();
            goto TryAgain;
        }

        switch (token.Id)
        {
        case TokenId.Struct:
        case TokenId.Class:
        case TokenId.Union:
        case TokenId.Interface:
        case TokenId.Enum:
            node = ReadDeclaration(attributes) with { Namespace = GetCurrentNamespace() };
            Expect(TokenId.Semicolon);
            break;
        case TokenId.Namespace:
            ReadNamespaceStart();
            goto TryAgain;
        case TokenId.Import:
            node = ReadImport();
            break;
        default:
            throw new InvalidDataException(token.Id.ToString());
        }

        return true;
    }

    private string? GetCurrentNamespace()
    {
        if (_namespaceDepth == 0)
            return null;

        return string.Join('.', _namespaces.Reverse());
    }

    private ImportNode ReadImport()
    {
        Expect(TokenId.Import);
        var literal = Expect(TokenId.StringLiteral).ToString(_text);
        Expect(TokenId.Semicolon);
        return new ImportNode(new StringLiteralNode(literal));
    }

    private void ReadNamespaceStart()
    {
        Expect(TokenId.Namespace);
        var namespaceName = Expect(TokenId.Identifier).ToString(_text);

        while (TryReadToken(TokenId.Period, out _))
            namespaceName += '.' + Expect(TokenId.Identifier).ToString(_text);

        Expect(TokenId.OpeningBrace);
        _namespaceDepth++;
        _namespaces.Push(namespaceName);
    }

    private bool TryReadAttributes(out ImmutableArray<AttributeNode> attributes)
    {
        if (!TryReadToken(TokenId.OpeningBracket, out _))
        {
            attributes = [];
            return false;
        }

        var builder = ImmutableArray.CreateBuilder<AttributeNode>();

        do
        {
            for (int i = 0; !TryReadToken(TokenId.ClosingBracket, out _); i++)
            {
                if (i > 0)
                    Expect(TokenId.Comma);

                var name = Expect(TokenId.Identifier).ToString(_text);
                var args = ImmutableArray.CreateBuilder<ExpressionNode>();

                if (TryReadToken(TokenId.OpeningParenthesis, out _))
                {
                    for (int j = 0; !TryReadToken(TokenId.ClosingParenthesis, out _); j++)
                    {
                        if (j > 0)
                            Expect(TokenId.Comma);

                        args.Add(ReadExpression());
                    }
                }

                var attr = new AttributeNode(args.DrainToImmutable(), name);
                builder.Add(attr);
            }
        } while (TryReadToken(TokenId.OpeningBracket, out _));

        attributes = builder.DrainToImmutable();
        return true;
    }

    private void ReadMemberBlock(ImmutableArray<MemberNode>.Builder builder)
    {
        Expect(TokenId.OpeningBrace);

        while (!TryReadToken(TokenId.ClosingBrace, out _))
        {
            TryReadAttributes(out var attributes);

            if (TryPeekToken(TokenId.OpeningBrace, out _))
            {
                var members = ImmutableArray.CreateBuilder<MemberNode>();
                ReadMemberBlock(members);
                builder.Add(new MemberBlockNode(attributes, members.DrainToImmutable()));
                continue;
            }

            builder.Add(ReadMember(attributes));
            Expect(TokenId.Semicolon);
        }
    }

    private void ReadEnumMemberBlock(ImmutableArray<EnumMemberNode>.Builder builder)
    {
        Expect(TokenId.OpeningBrace);

        for (int i = 0; !TryReadToken(TokenId.ClosingBrace, out _); i++)
        {
            if (i > 0)
                Expect(TokenId.Comma);

            if (TryReadToken(TokenId.ClosingBrace, out _))
                break;

            TryReadAttributes(out var attributes);
            builder.Add(ReadEnumMember(attributes));
        }
    }

    private MemberNode ReadMember(ImmutableArray<AttributeNode> attributes)
    {
        var position = _state.Position;
        var type = ReadType(out var name);

        if (type is TypeDeclarationNode)
            return new FieldNode(attributes, name, type, null);

        if (TryPeekToken(TokenId.OpeningParenthesis, out _))
        {
            ThrowIfNoIdentifier();
            return new MethodNode(attributes, name!, new MethodTypeNode(type, ReadParameterList(), TryReadToken(TokenId.Const, out _)));
        }

        if (TryPeekToken(TokenId.OpeningBracket, out _))
            type = ReadArray(type);

        if (TryReadToken(TokenId.Colon, out _))
            return new FieldNode(attributes, name, type, ReadExpression());

        void ThrowIfNoIdentifier()
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidDataException($"{position}: Expected identifier to name member.");
            }
        }

        ThrowIfNoIdentifier();
        return new FieldNode(attributes, name, type, BitWidth: null);
    }

    private EnumMemberNode ReadEnumMember(ImmutableArray<AttributeNode> attributes)
    {
        ExpressionNode? value = null;
        var name = Expect(TokenId.Identifier).ToString(_text);

        if (TryReadToken(TokenId.Equals, out _))
            value = ReadExpression();

        return new EnumMemberNode(attributes, name, value);
    }

    private TypeNode ReadArray(TypeNode elementType)
    {
        if (!TryReadToken(TokenId.OpeningBracket, out _))
            return elementType;

        var dimensions = new List<ExpressionNode?>();
        var arrayType = elementType;

        do
        {
            ExpressionNode? length = null;

            if (!TryPeekToken(TokenId.ClosingBracket, out _))
                length = ReadExpression();

            Expect(TokenId.ClosingBracket);
            dimensions.Add(length);

        } while (TryReadToken(TokenId.OpeningBracket, out _));

        for (int i = dimensions.Count - 1; i >= 0; i--)
            arrayType = new ArrayTypeNode(arrayType, dimensions[i]);

        return arrayType;
    }

    private ExpressionNode ReadExpression()
    {
        if (TryReadToken(TokenId.StringLiteral, out var token))
            return new StringLiteralNode(token.ToString(_text));

        if (TryReadToken(TokenId.Minus, out _))
            return new UnaryNegationNode(ReadExpression());

        if (TryReadToken(TokenId.Identifier, out token))
            return new IdentifierExpressionNode(token.ToString(_text));

        if (TryReadToken(TokenId.Number, out token))
        {
            var value = token.GetValue(_text);
            var style = NumberStyles.Integer;

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                style = NumberStyles.HexNumber;
                value = value[2..];
            }

            return new IntegerLiteralNode(long.Parse(value, style));
        }

        return Unexpected<ExpressionNode>(token);
    }

    private EnumNode ReadEnumDeclaration(ImmutableArray<AttributeNode> attributes)
    {
        Expect(TokenId.Enum);
        string? name = null;
        ImmutableArray<EnumMemberNode> members = [];
        var baseTypes = ImmutableArray.CreateBuilder<BaseTypeNode>();

        if (TryReadToken(TokenId.Identifier, out var token))
            name = token.ToString(_text);

        if (TryReadToken(TokenId.Colon, out _))
        {
            TryReadAttributes(out var baseTypeAttributes);
            baseTypes.Add(new BaseTypeNode(baseTypeAttributes, Expect(TokenId.Identifier).ToString(_text)));

            while (TryReadToken(TokenId.Comma, out _))
            {
                TryReadAttributes(out baseTypeAttributes);
                baseTypes.Add(new BaseTypeNode(baseTypeAttributes, Expect(TokenId.Identifier).ToString(_text)));
            }
        }

        if (TryPeekToken(TokenId.OpeningBrace, out _))
        {
            var builder = ImmutableArray.CreateBuilder<EnumMemberNode>();
            ReadEnumMemberBlock(builder);
            members = builder.DrainToImmutable();
        }

        return new EnumNode(attributes, name, baseTypes.DrainToImmutable(), members);
    }

    private TypeDeclarationNode ReadDeclaration(ImmutableArray<AttributeNode> attributes)
    {
        if (TryPeekToken(TokenId.Enum, out _))
            return ReadEnumDeclaration(attributes);

        string? name = null;
        var declType = Expect(TokenId.Interface, TokenId.Struct, TokenId.Class, TokenId.Union);
        var baseTypes = ImmutableArray.CreateBuilder<BaseTypeNode>();
        ImmutableArray<MemberNode> members = default;

        if (TryReadToken(TokenId.Identifier, out var token))
        {
            name = token.ToString(_text);

            if (TryReadToken(TokenId.Colon, out _))
            {
                TryReadAttributes(out var baseTypeAttributes);
                baseTypes.Add(new BaseTypeNode(baseTypeAttributes, Expect(TokenId.Identifier).ToString(_text)));

                while (TryReadToken(TokenId.Comma, out _))
                {
                    TryReadAttributes(out baseTypeAttributes);
                    baseTypes.Add(new BaseTypeNode(baseTypeAttributes, Expect(TokenId.Identifier).ToString(_text)));
                }
            }
        }

        if (TryPeekToken(TokenId.OpeningBrace, out _))
        {
            var builder = ImmutableArray.CreateBuilder<MemberNode>();
            ReadMemberBlock(builder);
            members = builder.DrainToImmutable();
        }

        return declType.Id switch
        {
            TokenId.Interface => new InterfaceNode(attributes, name!, baseTypes.DrainToImmutable(), members, false),
            TokenId.Union => new UnionNode(attributes, name, baseTypes.DrainToImmutable(), members, false),
            TokenId.Struct => new StructNode(attributes, name, baseTypes.DrainToImmutable(), members, false),
            _ /* Class */ => new ClassNode(attributes, name, baseTypes.DrainToImmutable(), members, false)
        };
    }

    private TypeNode ReadType(out string? identifierName)
    {
        Token token;
        TypeNode? type;
        string? name;
        bool isConst = false;

        if (TryReadToken(TokenId.Const, out _))
            isConst = true;

        if (TryPeekToken(TokenId.Union, out _) ||
            TryPeekToken(TokenId.Struct, out _) ||
            TryPeekToken(TokenId.Class, out _) ||
            TryPeekToken(TokenId.Enum, out _))
        {
            type = ReadDeclaration([]);
        }
        else
        {
            name = Expect(TokenId.Identifier).ToString(_text);
            type = new NamedTypeNode(name, isConst);
        }

        if (TryReadToken(TokenId.Const, out token))
        {
            if (isConst)
                Unexpected(token);

            isConst = true;
        }

        type = type with { IsConst = isConst };
        type = ReadPointers(type);

        if (TryReadToken(TokenId.Identifier, out token))
            identifierName = token.ToString(_text);
        else
            identifierName = null;

        return type;
    }

    private TypeNode ReadPointers(TypeNode type)
    {
        while (TryReadToken(TokenId.Asterisk, out var token) || TryReadToken(TokenId.Ampersand, out token))
        {
            bool isConst = false;

            if (TryReadToken(TokenId.Const, out _))
                isConst = true;

            if (token.Id == TokenId.Asterisk)
                type = new PointerTypeNode(type, isConst);
            else
                type = new ReferenceTypeNode(type, isConst);
        }

        return type;
    }

    private ImmutableArray<ParameterNode> ReadParameterList()
    {
        var nodes = ImmutableArray.CreateBuilder<ParameterNode>();
        Expect(TokenId.OpeningParenthesis);

        for (int i = 0; !TryReadToken(TokenId.ClosingParenthesis, out _); i++)
        {
            if (i > 0)
                Expect(TokenId.Comma);

            TryReadAttributes(out var attributes);
            var type = ReadType(out var name);

            if (TryPeekToken(TokenId.OpeningBracket, out _))
                type = ReadArray(type);

            nodes.Add(new ParameterNode(attributes, type, name));
        }

        return nodes.DrainToImmutable();
    }

    /// <summary>Gets the next token.</summary>
    private Token ReadToken()
    {
        if (!TryReadToken(out var token))
            throw new EndOfStreamException($"Expected token, got end of stream.");

        return token;
    }

    [DoesNotReturn]
    private static void Unexpected(Token token) => Unexpected<object>(token);

    [DoesNotReturn]
    private static T Unexpected<T>(Token token) => Unexpected<T>(token.Id.ToString(), token.Position);

    [DoesNotReturn]
    private static void Unexpected(string type, TextPosition position) => Unexpected<object>(type, position);

    [DoesNotReturn]
    private static T Unexpected<T>(string type, TextPosition position) => throw new InvalidDataException($"{position}: {type} was unexpected at this time.");

    [DoesNotReturn]
    private static void Expected(TokenId id, TextPosition position) => Expected<object>(id, position);

    [DoesNotReturn]
    private static T Expected<T>(TokenId id, TextPosition position) => throw new InvalidDataException($"{position}: Expected {id}.");

    /// <summary>Gets the next token and validates that has the provided token type.</summary>
    private Token Expect(TokenId id)
    {
        if (!TryReadToken(out var token))
            throw new EndOfStreamException($"{token.Position}: Expected {id}, got end of stream.");

        if (token.Id != id)
            throw new ArgumentException($"{token.Position}: Expected {id}, got {token.Id}.");

        return token;
    }

    /// <summary>Gets the next token and validates that has one of the provided token types.</summary>
    private Token Expect(params ReadOnlySpan<TokenId> ids)
    {
        if (!TryReadToken(out var token))
            throw new EndOfStreamException($"Expected token, got end of stream.");

        foreach (var id in ids)
        {
            if (token.Id == id)
            {
                return token;
            }
        }

        throw new ArgumentException($"{token.Position}: Expected {string.Join(", ", ids[..^1].ToArray())} or {ids[^1]}, got {token.Id}.");
    }

    /// <summary>Gets the next token.</summary>
    private bool TryReadToken(out Token token)
    {
        var lexer = new TokenReader(_text, _state);
        var result = lexer.GetNextToken(out token);
        _state = lexer.State;
        return result;
    }

    /// <summary>Gets the next token.</summary>
    private bool TryReadToken(TokenId id, out Token token)
    {
        var lexer = new TokenReader(_text, _state);
        
        if (lexer.GetNextToken(out token) && token.Id == id)
        {
            _state = lexer.State;
            return true;
        }

        return false;
    }

    /// <summary>Gets the next token without advancing the text position.</summary>
    private bool TryPeekToken(out Token token)
    {
        var lexer = new TokenReader(_text, _state);
        return lexer.GetNextToken(out token);
    }

    /// <summary>Gets the next token without advancing the text position.</summary>
    private bool TryPeekToken(TokenId id, out Token token)
    {
        var lexer = new TokenReader(_text, _state);
        return lexer.GetNextToken(out token) && token.Id == id;
    }
}
