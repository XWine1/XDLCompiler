namespace XDLCompiler;

public sealed class NoEmitAttribute : IAttributeFactory<NoEmitAttribute>
{
    static string IAttributeFactory<NoEmitAttribute>.Name => "no_emit";

    static NoEmitAttribute IAttributeFactory<NoEmitAttribute>.Create(AttributeNode node) => new();
}
