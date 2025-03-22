using System.Collections.Immutable;

namespace XDLCompiler;

/// <summary></summary>
public interface INamedNode
{
    string? Name { get; }
}

/// <summary></summary>
public interface IAttributableNode
{
    ImmutableArray<AttributeNode> Attributes { get; }
}

/// <summary>Base class for all syntax nodes.</summary>
public abstract record SyntaxNode
{
    /// <summary>Accepts a visitor for traversing the syntax tree</summary>
    public abstract void Accept(ISyntaxVisitor visitor);
}

public sealed record ImportNode(StringLiteralNode FileName) : SyntaxNode
{
    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

/// <summary>A syntax node representing an attribute.</summary>
public sealed record AttributeNode(ImmutableArray<ExpressionNode> Arguments, string Name) : SyntaxNode, INamedNode
{
    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

public abstract record TypeDeclarationNode(
    ImmutableArray<AttributeNode> Attributes,
    string? Name,
    ImmutableArray<BaseTypeNode> BaseTypes,
    ImmutableArray<MemberNode> Members,
    bool IsConst = false,
    string? Namespace = null) : TypeNode(IsConst), IAttributableNode, INamedNode
{
    public abstract string DeclarationType { get; }
}

public sealed record EnumMemberNode(
    ImmutableArray<AttributeNode> Attributes,
    string Name,
    ExpressionNode? Value) : MemberNode(Attributes, Name)
{
    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

public sealed record EnumNode(
    ImmutableArray<AttributeNode> Attributes,
    string? Name,
    ImmutableArray<BaseTypeNode> BaseTypes,
    ImmutableArray<EnumMemberNode> EnumMembers,
    bool IsConst = false,
    string? Namespace = null) : TypeDeclarationNode(Attributes, Name, BaseTypes, [..EnumMembers], IsConst, Namespace)
{
    /// <inheritdoc/>
    public override string DeclarationType => "enum";

    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

public sealed record InterfaceNode(
    ImmutableArray<AttributeNode> Attributes,
    string Name,
    ImmutableArray<BaseTypeNode> BaseTypes,
    ImmutableArray<MemberNode> Members,
    bool IsConst = false,
    string? Namespace = null) : TypeDeclarationNode(Attributes, Name, BaseTypes, Members, IsConst, Namespace)
{
    /// <inheritdoc/>
    public override string DeclarationType => "interface";

    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

public sealed record UnionNode(
    ImmutableArray<AttributeNode> Attributes,
    string? Name,
    ImmutableArray<BaseTypeNode> BaseTypes,
    ImmutableArray<MemberNode> Members,
    bool IsConst = false,
    string? Namespace = null) : TypeDeclarationNode(Attributes, Name, BaseTypes, Members, IsConst, Namespace)
{
    /// <inheritdoc/>
    public override string DeclarationType => "union";

    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

public sealed record StructNode(
    ImmutableArray<AttributeNode> Attributes,
    string? Name,
    ImmutableArray<BaseTypeNode> BaseTypes,
    ImmutableArray<MemberNode> Members,
    bool IsConst = false,
    string? Namespace = null) : TypeDeclarationNode(Attributes, Name, BaseTypes, Members, IsConst, Namespace)
{
    /// <inheritdoc/>
    public override string DeclarationType => "struct";

    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

public sealed record ClassNode(
    ImmutableArray<AttributeNode> Attributes,
    string? Name,
    ImmutableArray<BaseTypeNode> BaseTypes,
    ImmutableArray<MemberNode> Members,
    bool IsConst = false,
    string? Namespace = null) : TypeDeclarationNode(Attributes, Name, BaseTypes, Members, IsConst, Namespace)
{
    /// <inheritdoc/>
    public override string DeclarationType => "class";

    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

/// <summary>Represents a base type in a type hierarchy.</summary>
public sealed record BaseTypeNode(ImmutableArray<AttributeNode> Attributes, string Name) : SyntaxNode, IAttributableNode, INamedNode
{
    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

/// <summary>Base class for nodes representing members of a type.</summary>
public abstract record MemberNode(ImmutableArray<AttributeNode> Attributes, string? Name, bool IsStatic = false) : SyntaxNode, IAttributableNode, INamedNode
{
}

public sealed record MemberBlockNode(ImmutableArray<AttributeNode> Attributes, ImmutableArray<MemberNode> Members) : MemberNode(Attributes, string.Empty, false)
{
    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

/// <summary>A syntax node representing a field on a type.</summary>
public sealed record FieldNode(ImmutableArray<AttributeNode> Attributes, string? Name, TypeNode Type, ExpressionNode? BitWidth, bool IsStatic = false) : MemberNode(Attributes, Name, IsStatic)
{
    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

public sealed record MethodTypeNode(
    TypeNode ReturnType,
    ImmutableArray<ParameterNode> Parameters,
    bool IsConst = false) : TypeNode(IsConst)
{
    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

/// <summary>A syntax node representing a method on a type.</summary>
public sealed record MethodNode(
    ImmutableArray<AttributeNode> Attributes,
    string Name,
    MethodTypeNode Signature,
    bool IsStatic = false,
    bool ForceInline = false) : MemberNode(Attributes, Name, IsStatic)
{
    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

/// <summary>A dummy node representing the end of a member block, for member enumeration.</summary>
public sealed record MemberBlockEndNode(MemberBlockNode Block) : MemberNode([], null)
{
    public override void Accept(ISyntaxVisitor visitor)
    {
    }
}

/// <summary>A syntax mode representing a method parameter.</summary>
public sealed record ParameterNode(ImmutableArray<AttributeNode> Attributes, TypeNode Type, string? Name) : SyntaxNode, IAttributableNode, INamedNode
{
    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

/// <summary>Base class for all type reference nodes.</summary>
public abstract record TypeNode(bool IsConst = false) : SyntaxNode
{
}

/// <summary>A syntax node representing a reference to a named type.</summary>
public sealed record NamedTypeNode(string Name, bool IsConst = false) : TypeNode(IsConst), INamedNode
{
    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

public abstract record PointerOrReferenceTypeNode(TypeNode ElementType, bool IsConst = false) : TypeNode(IsConst)
{
}

/// <summary>A syntax node representing a typed pointer.</summary>
public sealed record PointerTypeNode(TypeNode ElementType, bool IsConst = false) : PointerOrReferenceTypeNode(ElementType, IsConst)
{
    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

/// <summary>A syntax node representing a typed reference.</summary>
public sealed record ReferenceTypeNode(TypeNode ElementType, bool IsConst = false) : PointerOrReferenceTypeNode(ElementType, IsConst)
{
    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

public sealed record ArrayTypeNode(TypeNode ElementType, ExpressionNode? Length, bool IsConst = false) : TypeNode(IsConst)
{
    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

/// <summary>Base class for all expression nodes.</summary>
public abstract record ExpressionNode : SyntaxNode
{
    /// <summary>Evaluates a constant expression.</summary>
    public abstract object Evaluate();
}

public abstract record LiteralNode : ExpressionNode
{
}

public sealed record StringLiteralNode(string Value) : LiteralNode
{
    /// <inheritdoc/>
    public override object Evaluate() => ToString();

    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);

    /// <inheritdoc/>
    public override string ToString() => Value[1..^1]; // TODO: escape chars
}

public sealed record IntegerLiteralNode(long Value) : LiteralNode
{
    /// <inheritdoc/>
    public override object Evaluate() => Value;

    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

public sealed record IdentifierExpressionNode(string Name) : ExpressionNode
{
    /// <inheritdoc/>
    public override object Evaluate() => Name;

    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}

public abstract record UnaryExpressionNode(ExpressionNode Operand) : ExpressionNode
{
}

public sealed record UnaryNegationNode(ExpressionNode Operand) : UnaryExpressionNode(Operand)
{
    /// <inheritdoc/>
    public override object Evaluate() => -(long)Operand.Evaluate();

    /// <inheritdoc/>
    public override void Accept(ISyntaxVisitor visitor) => visitor.Visit(this);
}
