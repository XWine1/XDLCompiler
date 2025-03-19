using XDLCompiler;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Immutable;

public static class SyntaxNodeExtensions
{
    private static readonly Version s_Version0 = new(0, 0, 0, 0);

    public static bool TryGetAttribute<TAttribute>(this IAttributableNode node, [NotNullWhen(true)] out TAttribute? attribute)
        where TAttribute : class, IAttributeFactory<TAttribute>
    {
        foreach (var attributeNode in node.Attributes)
        {
            if (attributeNode.TryGetAttribute(out attribute))
            {
                return true;
            }
        }

        attribute = null;
        return false;
    }

    public static bool TryGetAttribute<TAttribute>(this AttributeNode node, [NotNullWhen(true)] out TAttribute? attribute)
        where TAttribute : class, IAttributeFactory<TAttribute>
    {
        if (node.Name == TAttribute.Name)
        {
            attribute = TAttribute.Create(node);
            return true;
        }

        attribute = null;
        return false;
    }

    public static bool Exists(this IAttributableNode node, Version? version)
    {
        version ??= s_Version0;

        if (node.TryGetAttribute(out AbiAddedAttribute? added) && added.Version > version)
            return false;

        if (node.TryGetAttribute(out AbiRemovedAttribute? removed) && removed.Version <= version)
            return false;

        return true;
    }

    public static IEnumerable<MemberNode> EnumerateAllMembers(this TypeDeclarationNode node, bool includeBlocks = false)
    {
        return EnumerateAllMembers(node.Members, includeBlocks);
    }

    public static IEnumerable<MemberNode> EnumerateMembers(this TypeDeclarationNode node, Version? abi, bool includeBlocks = false)
    {
        return EnumerateMembers(node.Members, abi, includeBlocks);
    }

    public static IEnumerable<MemberNode> EnumerateAllMembers(this MemberBlockNode node, bool includeBlocks = false)
    {
        return EnumerateAllMembers(node.Members, includeBlocks);
    }

    public static IEnumerable<MemberNode> EnumerateMembers(this MemberBlockNode node, Version? abi, bool includeBlocks = false)
    {
        return EnumerateMembers(node.Members, abi, includeBlocks);
    }

    private static IEnumerable<MemberNode> EnumerateAllMembers(ImmutableArray<MemberNode> members, bool includeBlocks)
    {
        foreach (var member in members)
        {
            if (member is MemberBlockNode block)
            {
                if (includeBlocks)
                    yield return block;

                foreach (var node in EnumerateAllMembers(block.Members, includeBlocks))
                    yield return node;

                if (includeBlocks)
                    yield return new MemberBlockEndNode(block);

                continue;
            }

            yield return member;
        }
    }

    private static IEnumerable<MemberNode> EnumerateMembers(ImmutableArray<MemberNode> members, Version? abi, bool includeBlocks)
    {
        foreach (var member in members)
        {
            if (!member.Exists(abi))
                continue;

            if (member is MemberBlockNode block)
            {
                if (includeBlocks)
                    yield return block;

                foreach (var node in EnumerateAllMembers(block.Members, includeBlocks))
                {
                    if (!node.Exists(abi))
                        continue;

                    yield return node;
                }

                if (includeBlocks)
                    yield return new MemberBlockEndNode(block);

                continue;
            }

            yield return member;
        }
    }
}
