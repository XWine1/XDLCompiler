using XDLCompiler;

public interface IAttributeFactory<TAttribute>
    where TAttribute : IAttributeFactory<TAttribute>
{
    static abstract string Name { get; }

    static abstract TAttribute Create(AttributeNode node);
}
