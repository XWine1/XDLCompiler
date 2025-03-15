namespace XDLCompiler;

public sealed class NoUuidAttribute : IAttributeFactory<NoUuidAttribute>
{
    static string IAttributeFactory<NoUuidAttribute>.Name => "no_uuid";

    static NoUuidAttribute IAttributeFactory<NoUuidAttribute>.Create(AttributeNode node) => new();
}
