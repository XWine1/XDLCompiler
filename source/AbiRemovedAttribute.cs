namespace XDLCompiler;

public sealed class AbiRemovedAttribute(AttributeNode node) : IAttributeFactory<AbiRemovedAttribute>
{
    public Version Version { get; } = new(
        (int)(long)node.Arguments[0].Evaluate(),
        (int)(long)node.Arguments[1].Evaluate(),
        (int)(long)node.Arguments[2].Evaluate(),
        (int)(long)node.Arguments[3].Evaluate());

    static string IAttributeFactory<AbiRemovedAttribute>.Name => "removed";

    static AbiRemovedAttribute IAttributeFactory<AbiRemovedAttribute>.Create(AttributeNode node) => new(node);
}
