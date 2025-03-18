namespace XDLCompiler;

public sealed class RcxReturnAttribute : IAttributeFactory<RcxReturnAttribute>
{
    static string IAttributeFactory<RcxReturnAttribute>.Name => "rcx_return";

    static RcxReturnAttribute IAttributeFactory<RcxReturnAttribute>.Create(AttributeNode node) => new();
}
