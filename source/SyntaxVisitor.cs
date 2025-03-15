namespace XDLCompiler;

public abstract class SyntaxVisitor : ISyntaxVisitor
{
    public virtual void Visit(ImportNode node)
    {
    }

    public virtual void Visit(AttributeNode node)
    {
        foreach (var argument in node.Arguments)
        {
            argument.Accept(this);
        }
    }

    public virtual void Visit(EnumNode node)
    {
        foreach (var attribute in node.Attributes)
        {
            attribute.Accept(this);
        }

        node.BaseType?.Accept(this);

        foreach (var member in node.Members)
        {
            member.Accept(this);
        }
    }

    public virtual void Visit(EnumMemberNode node)
    {
        foreach (var attribute in node.Attributes)
        {
            attribute.Accept(this);
        }

        node.Value?.Accept(this);
    }

    public virtual void Visit(InterfaceNode node)
    {
        Visit((TypeDeclarationNode)node);
    }

    public virtual void Visit(UnionNode node)
    {
        Visit((TypeDeclarationNode)node);
    }

    public virtual void Visit(StructNode node)
    {
        Visit((TypeDeclarationNode)node);
    }

    public virtual void Visit(ClassNode node)
    {
        Visit((TypeDeclarationNode)node);
    }

    public virtual void Visit(TypeDeclarationNode node)
    {
        foreach (var attribute in node.Attributes)
        {
            attribute.Accept(this);
        }

        foreach (var baseType in node.BaseTypes)
        {
            baseType.Accept(this);
        }

        if (!node.Members.IsDefaultOrEmpty)
        {
            foreach (var member in node.Members)
            {
                member.Accept(this);
            }
        }
    }

    public virtual void Visit(BaseTypeNode node)
    {
        foreach (var attribute in node.Attributes)
        {
            attribute.Accept(this);
        }
    }

    public virtual void Visit(MemberBlockNode node)
    {
        foreach (var attribute in node.Attributes)
        {
            attribute.Accept(this);
        }

        foreach (var member in node.Members)
        {
            member.Accept(this);
        }
    }

    public virtual void Visit(FieldNode node)
    {
        foreach (var attribute in node.Attributes)
        {
            attribute.Accept(this);
        }

        node.Type.Accept(this);
    }

    public virtual void Visit(MethodNode node)
    {
        foreach (var attribute in node.Attributes)
        {
            attribute.Accept(this);
        }

        node.Signature.Accept(this);
    }

    public virtual void Visit(MethodTypeNode node)
    {
        foreach (var parameter in node.Parameters)
        {
            parameter.Accept(this);
        }

        node.ReturnType.Accept(this);
    }

    public virtual void Visit(ParameterNode node)
    {
        foreach (var attribute in node.Attributes)
        {
            attribute.Accept(this);
        }

        node.Type.Accept(this);
    }

    public virtual void Visit(PointerTypeNode node)
    {
        node.ElementType.Accept(this);
    }

    public virtual void Visit(ReferenceTypeNode node)
    {
        node.ElementType.Accept(this);
    }

    public virtual void Visit(ArrayTypeNode node)
    {
        node.ElementType.Accept(this);
    }

    public virtual void Visit(NamedTypeNode node)
    {
    }

    public virtual void Visit(IdentifierExpressionNode node)
    {
    }

    public virtual void Visit(IntegerLiteralNode node)
    {
    }

    public virtual void Visit(StringLiteralNode node)
    {
    }

    public virtual void Visit(UnaryNegationNode node)
    {
        node.Operand.Accept(this);
    }
}
