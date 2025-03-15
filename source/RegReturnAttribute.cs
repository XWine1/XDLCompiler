namespace XDLCompiler;

public sealed class RegReturnAttribute : IAttributeFactory<RegReturnAttribute>
{
    static string IAttributeFactory<RegReturnAttribute>.Name => "reg_return";

    static RegReturnAttribute IAttributeFactory<RegReturnAttribute>.Create(AttributeNode node) => new();
}
