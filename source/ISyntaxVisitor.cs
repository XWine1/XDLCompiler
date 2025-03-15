namespace XDLCompiler;

public interface ISyntaxVisitor
{
    void Visit(AttributeNode node);
    void Visit(InterfaceNode node);
    void Visit(EnumNode node);
    void Visit(UnionNode node);
    void Visit(StructNode node);
    void Visit(ClassNode node);
    void Visit(BaseTypeNode node);
    void Visit(MemberBlockNode node);
    void Visit(FieldNode node);
    void Visit(EnumMemberNode node);
    void Visit(MethodNode node);
    void Visit(ParameterNode node);
    void Visit(PointerTypeNode node);
    void Visit(ReferenceTypeNode node);
    void Visit(NamedTypeNode node);
    void Visit(MethodTypeNode node);
    void Visit(IdentifierExpressionNode node);
    void Visit(IntegerLiteralNode node);
    void Visit(StringLiteralNode node);
    void Visit(UnaryNegationNode node);
    void Visit(ArrayTypeNode node);
    void Visit(ImportNode node);
}
