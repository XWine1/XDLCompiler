namespace XDLCompiler;

public sealed class UuidAttribute(AttributeNode node) : IAttributeFactory<UuidAttribute>
{
    public Guid Guid { get; } = Guid.Parse((string)node.Arguments[0].Evaluate());

    static string IAttributeFactory<UuidAttribute>.Name => "uuid";

    static UuidAttribute IAttributeFactory<UuidAttribute>.Create(AttributeNode node) => new(node);
}
