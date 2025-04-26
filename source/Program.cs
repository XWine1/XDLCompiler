using XDLCompiler;
using System.CodeDom.Compiler;

Run(args);

static void Run(string[] args)
{
    if (args.Length < 2)
    {
        Console.WriteLine("usage: xdl input.xdl output_dir [prefix]");
        return;
    }

    var generator = new HeaderGenerator();
    var headerName = generator.GetHeaderName(args[0]);

    Directory.CreateDirectory(args[1]);

    using (var file = new StreamWriter(File.Create(Path.Combine(args[1], headerName))))
    using (var writer = new IndentedTextWriter(file))
    {
        generator.GenerateHeader(args[0], args.Length > 2 ? args[2] : null, writer);
    }

    if (args.Length > 2)
    {
        using var file = new StreamWriter(File.Create(Path.Combine(args[1], "impls_" + headerName)));
        using var writer = new IndentedTextWriter(file);
        generator.GenerateImpls(args[2], writer);
    }
}
