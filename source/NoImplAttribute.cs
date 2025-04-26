namespace XDLCompiler;

public sealed class NoImplAttribute : IAttributeFactory<NoImplAttribute>
{
    static string IAttributeFactory<NoImplAttribute>.Name => "no_impl";

    static NoImplAttribute IAttributeFactory<NoImplAttribute>.Create(AttributeNode node) => new();
}
