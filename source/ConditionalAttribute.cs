namespace XDLCompiler;

public sealed class ConditionalAttribute(AttributeNode node) : IAttributeFactory<ConditionalAttribute>
{
    public string Condition { get; } = node.Arguments[0].Evaluate().ToString()!;

    static string IAttributeFactory<ConditionalAttribute>.Name => "conditional";

    static ConditionalAttribute IAttributeFactory<ConditionalAttribute>.Create(AttributeNode node) => new(node);
}
