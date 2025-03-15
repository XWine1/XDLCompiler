namespace XDLCompiler;

public sealed class AbiAddedAttribute(AttributeNode node) : IAttributeFactory<AbiAddedAttribute>
{
    public Version Version { get; } = new(
        (int)(long)node.Arguments[0].Evaluate(),
        (int)(long)node.Arguments[1].Evaluate(),
        (int)(long)node.Arguments[2].Evaluate(),
        (int)(long)node.Arguments[3].Evaluate());

    static string IAttributeFactory<AbiAddedAttribute>.Name => "added";

    static AbiAddedAttribute IAttributeFactory<AbiAddedAttribute>.Create(AttributeNode node) => new(node);
}
