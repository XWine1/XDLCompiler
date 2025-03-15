namespace XDLCompiler;

public sealed class ForceAbiAttribute : IAttributeFactory<ForceAbiAttribute>
{
    static string IAttributeFactory<ForceAbiAttribute>.Name => "force_abi";

    static ForceAbiAttribute IAttributeFactory<ForceAbiAttribute>.Create(AttributeNode node) => new();
}
