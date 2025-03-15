using System.CodeDom.Compiler;

namespace XDLCompiler;

public static class CFormatter
{
    public static void Write(MemberNode node, TextWriter writer, Dictionary<string, string> remap)
    {
        if (node is FieldNode field)
            Write(field, writer, remap);
        else if (node is MethodNode method)
            Write(method, writer, remap);
    }

    public static void Write(FieldNode node, TextWriter writer, Dictionary<string, string> remap)
    {
        if (Write(node.Type, writer, remap, node.Name) && !string.IsNullOrEmpty(node.Name))
        {
            if (node.Type is not ArrayTypeNode)
            {
                writer.Write(' ');
            }
        }

        if (node.Type is not ArrayTypeNode)
            writer.Write(node.Name);

        if (node.BitWidth != null)
        {
            writer.Write(" : ");
            writer.Write(node.BitWidth.Evaluate());
        }
    }

    public static void Write(ParameterNode node, TextWriter writer, Dictionary<string, string> remap)
    {
        if (Write(node.Type, writer, remap, node.Name) && !string.IsNullOrEmpty(node.Name))
        {
            if (node.Type is not ArrayTypeNode)
            {
                writer.Write(' ');
            }
        }

        if (node.Type is not ArrayTypeNode)
        {
            writer.Write(node.Name);
        }
    }

    public static void Write(MethodNode node, TextWriter writer, Dictionary<string, string> remap)
    {
        if (node.IsStatic)
            writer.Write("static ");

        if (node.ForceInline)
            writer.Write("FORCEINLINE ");

        if (node.TryGetAttribute<RegReturnAttribute>(out _))
        {
            writer.Write("reg_return_t<");
            Write(node.Signature.ReturnType, writer, remap, identifier: null);
            writer.Write("> ");
        }
        else if (Write(node.Signature.ReturnType, writer, remap, identifier: null))
        {
            writer.Write(' ');
        }

        writer.Write(node.Name);
        writer.Write('(');

        for (int i = 0; i < node.Signature.Parameters.Length; i++)
        {
            if (i > 0)
                writer.Write(", ");

            Write(node.Signature.Parameters[i], writer, remap);
        }

        writer.Write(')');
    }

    // Returns whether a space is needed to separate from an identifier

    public static bool Write(TypeNode node, TextWriter writer, Dictionary<string, string> remap, string? identifier)
    {
        if (node is PointerOrReferenceTypeNode pointer)
            return Write(pointer, writer, remap, identifier);

        if (node is NamedTypeNode named)
            return Write(named, writer, remap);

        if (node is TypeDeclarationNode union)
            return Write(union, writer, remap);

        if (node is ArrayTypeNode array)
            return Write(array, writer, remap, identifier);

        return false;
    }

    public static bool Write(TypeDeclarationNode node, TextWriter writer, Dictionary<string, string> remap)
    {
        return WriteCore(node, new IndentedTextWriter(writer), remap);

        static bool WriteCore(TypeDeclarationNode node, IndentedTextWriter writer, Dictionary<string, string> remap)
        {
            writer.Write(node.DeclarationType);

            if (!string.IsNullOrEmpty(node.Name))
            {
                writer.Write(' ');

                if (remap.TryGetValue(node.Name, out var name))
                    writer.Write(name);
                else
                    writer.Write(node.Name);
            }

            if (node.Members.IsDefault)
                return true;

            writer.WriteLine();
            writer.WriteLine('{');
            writer.Indent++;

            foreach (var member in node.Members)
            {
                Write(member, writer, remap);
                writer.WriteLine(';');
            }

            writer.Indent--;
            writer.Write('}');

            if (node.IsConst)
            {
                writer.Write(' ');
                writer.Write("const");
            }    

            return true;
        }
    }

    public static bool Write(NamedTypeNode node, TextWriter writer, Dictionary<string, string> remap)
    {
        if (remap.TryGetValue(node.Name, out var name))
            writer.Write(name);
        else
            writer.Write(node.Name);

        if (node.IsConst)
            writer.Write(" const");

        return true;
    }

    public static bool Write(PointerOrReferenceTypeNode node, TextWriter writer, Dictionary<string, string> remap, string? identifier)
    {
        if (node.ElementType is MethodTypeNode method)
        {
            Write(method.ReturnType, writer, remap, identifier);
            writer.Write('(');
            writer.Write(node is PointerTypeNode ? '*' : '&');

            if (!string.IsNullOrEmpty(identifier))
                writer.Write(identifier);

            writer.Write(')');
            writer.Write('(');

            for (int i = 0; i < method.Parameters.Length; i++)
            {
                if (i > 0)
                    writer.Write(", ");

                Write(method.Parameters[i], writer, remap);
            }

            writer.Write(')');

            if (node.IsConst)
                writer.Write(" const");

            return true;
        }

        Write(node.ElementType, writer, remap, identifier);

        if (node.ElementType.IsConst || node.ElementType is not PointerOrReferenceTypeNode)
            writer.Write(' ');

        writer.Write(node is PointerTypeNode ? '*' : '&');

        if (node.IsConst)
        {
            writer.Write("const");
            return true;
        }

        return false;
    }

    public static bool Write(ArrayTypeNode node, TextWriter writer, Dictionary<string, string> remap, string? identifier)
    {
        var elementType = node.ElementType;

        while (elementType is ArrayTypeNode array)
            elementType = array.ElementType;

        bool needSpace = Write(elementType, writer, remap, null);

        if (!string.IsNullOrEmpty(identifier))
        {
            if (needSpace)
                writer.Write(' ');

            writer.Write(identifier);
        }

        for (var array = node; array != null; array = array.ElementType as ArrayTypeNode)
        {
            writer.Write('[');

            if (array.Length != null)
                writer.Write(array.Length.Evaluate());

            writer.Write(']');
        }

        return false;
    }
}
