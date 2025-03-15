using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;

namespace XDLCompiler;

public class HeaderGenerator
{
    private readonly List<ImportNode> _imports = [];

    private readonly Dictionary<string, TypeDeclarationNode> _types = [];

    private readonly Dictionary<string, TypeDeclarationNode> _importedTypes = [];

    private readonly Dictionary<string, TypeDeclarationNode> _allTypes = [];

    private readonly HashSet<string> _abiTypes = [];

    private readonly Dictionary<string, string> _remap = [];

    private readonly HashSet<Version> _versions = [];

    private readonly HashSet<string> _imported = [];

    private void ParseFile(string filePath, Dictionary<string, TypeDeclarationNode> types)
    {
        var source = File.ReadAllText(filePath);
        var reader = new XdlReader(source);
        var fullPath = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? Environment.CurrentDirectory;

        while (reader.TryReadNext(out SyntaxNode? node))
        {
            if (node is ImportNode import)
            {
                var path = Path.GetFullPath(Path.Combine(fullPath, import.FileName.ToString()));

                if (_imported.Contains(path))
                    continue;

                _imports.Add(import);
                _imported.Add(path);
                ParseFile(path, _importedTypes);
            }
            else if (node is TypeDeclarationNode declaration)
            {
                types.Add(declaration.Name!, declaration);
                _allTypes.TryAdd(declaration.Name!, declaration);
            }
        }
    }

    public virtual string GetHeaderName(string xdlName)
    {
        return Path.GetFileNameWithoutExtension(xdlName) + ".g.h";
    }

    public void GenerateHeader(string fileName, string? factoryName, IndentedTextWriter writer)
    {
        ParseFile(fileName, _types);
        var headerName = GetHeaderName(fileName);
        var includeGuard = "__" + headerName.Replace('.', '_') + "__";
        writer.WriteLine("#pragma once");
        writer.WriteLine($"#ifndef {includeGuard}");
        writer.WriteLine($"#define {includeGuard}");
        writer.WriteLine();
        writer.WriteLine("#include <xcom/base.h>");

        foreach (var (i, import) in _imports.Index())
        {
            if (i == 0)
                writer.WriteLine();

            var includeName = GetHeaderName(import.FileName.ToString());
            writer.WriteLine($"#include \"{includeName}\"");
        }

        void AddAbiType(TypeDeclarationNode node)
        {
            var name = node.Name!;
            var vtbl = name + "Vtbl";
            _abiTypes.Add(name);
            _abiTypes.Add(vtbl);
            _remap.TryAdd(name, GetQualifiedName(node) + "<ABI>");
            _remap.TryAdd(vtbl, GetQualifiedName(node) + "Vtbl<ABI>");
        }

        foreach (var node in _allTypes.Values)
        {
            _versions.Clear();
            node.Accept(new AbiVersionVisitor(_allTypes, _versions));

            // If the type has any ABI attributes, it is considered an ABI type.
            // This means it is templated and needs to be referenced with <ABI>.
            if (_versions.Count > 0 || node.TryGetAttribute<ForceAbiAttribute>(out _))
            {
                AddAbiType(node);
            }
        }

        foreach (var node in _types.Values)
        {
            var isAbi = new StrongBox<bool>();
            node.Accept(new HasAbiTypesVisitor(_abiTypes, isAbi));

            // If the type has any ABI type members, it is considered an ABI type.
            if (isAbi.Value)
            {
                AddAbiType(node);
            }
        }

        foreach (var node in _allTypes.Values)
        {
            _remap.TryAdd(node.Name!, GetQualifiedName(node)!);
            _remap.TryAdd(node.Name! + "Vtbl", GetQualifiedName(node)! + "Vtbl");
        }

        WriteTypes(writer);
        writer.WriteLine();
        WriteInterfaceTraits(writer);

        // Determine all used ABIs
        _versions.Clear();

        foreach (var node in _types.Values)
            node.Accept(new AbiVersionVisitor(_allTypes, _versions));

        if (!string.IsNullOrEmpty(factoryName))
            GenerateFactory(writer, factoryName);

        writer.WriteLine();
        writer.WriteLine($"#endif // {includeGuard}");
    }

    public void WriteTypes(IndentedTextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        string? currentNamespace = null;

        void SetNamespace(string? ns)
        {
            if (currentNamespace == ns)
                return;

            if (currentNamespace != null)
            {
                writer.Indent--;
                writer.WriteLine('}');

                if (!string.IsNullOrEmpty(ns))
                {
                    writer.WriteLine();
                }
            }

            if (!string.IsNullOrEmpty(ns))
            {
                writer.WriteLine($"namespace {ns.Replace(".", "::")}");
                writer.WriteLine('{');
                writer.Indent++;
            }

            currentNamespace = ns;
        }

        writer.WriteLine();

        // Forward declarations
        foreach (var node in _types.Values.OfType<TypeDeclarationNode>())
        {
            if (node.TryGetAttribute<NoEmitAttribute>(out _))
                continue;

            SetNamespace(node.Namespace);

            if (_abiTypes.Contains(node.Name!))
                writer.WriteLine("template<abi_t ABI>");

            if (node is InterfaceNode)
                writer.Write("struct");
            else
                writer.Write(node.DeclarationType);

            writer.Write(' ');
            writer.Write(node.Name);
            writer.WriteLine(';');
            writer.WriteLine();

            if (node is InterfaceNode)
            {
                if (_abiTypes.Contains(node.Name!))
                    writer.WriteLine("template<abi_t ABI>");

                writer.WriteLine($"struct {node.Name}Vtbl;");
                writer.WriteLine();
            }
        }

        int written = 0;

        foreach (var (i, node) in _types.Values.OfType<TypeDeclarationNode>().Index())
        {
            if (node.TryGetAttribute<NoEmitAttribute>(out _))
                continue;

            SetNamespace(node.Namespace);

            if (written > 0)
                writer.WriteLine();

            WriteDeclaration(node, writer);
            written++;
        }

        SetNamespace(null);
    }

    public void WriteInterfaceTraits(IndentedTextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        foreach (var node in _types.Values.OfType<InterfaceNode>())
        {
            if (node.TryGetAttribute<NoEmitAttribute>(out _))
                continue;

            if (node.TryGetAttribute(out UuidAttribute? uuid))
            {
                var guid = Unsafe.BitCast<Guid, RawGuid>(uuid.Guid);

                if (_abiTypes.Contains(node.Name!))
                    writer.Write("DECLARE_ABI_UUIDOF_HELPER");
                else
                    writer.Write("DECLARE_UUIDOF_HELPER");

                writer.Write($"({GetQualifiedName(node)}, ");
                writer.Write($"0x{guid.A:X8},0x{guid.B:X4},0x{guid.C:X4},");
                writer.Write($"0x{guid.D:X2},0x{guid.E:X2},0x{guid.F:X2},0x{guid.G:X2},");
                writer.Write($"0x{guid.H:X2},0x{guid.I:X2},0x{guid.J:X2},0x{guid.K:X2}");
                writer.WriteLine(")");
            }
            else if (!node.TryGetAttribute<NoUuidAttribute>(out _))
            {
                throw new InvalidOperationException($"{node.Name} is missing a [uuid] attribute!");
            }
            else
            {
                continue;
            }

            writer.WriteLine();
        }
    }

    public void GenerateImpls(IndentedTextWriter writer)
    {
        var remap2 = new Dictionary<string, string>();

        foreach (var node in _types.Values.OfType<TypeDeclarationNode>())
        {
            if (node.TryGetAttribute<NoEmitAttribute>(out _))
                continue;

            var name = GetQualifiedName(node)!;
            var vtbl = name + "Vtbl";

            if (_abiTypes.Contains(node.Name!))
            {
                name += "<ABI>";
                vtbl += "<ABI>";
            }

            remap2.TryAdd(node.Name!, name);

            if (node is InterfaceNode)
            {
                remap2.Add(node.Name + "Vtbl", vtbl);
            }
        }

        foreach (var (i, node) in _types.Values.OfType<InterfaceNode>().Index())
        {
            if (i > 0)
                writer.WriteLine();

            writer.WriteLine("//");
            writer.WriteLine($"// {node.Name}");
            writer.WriteLine("//");

            foreach (var (j, method) in node.EnumerateAllMembers().OfType<MethodNode>().Index())
            {
                if (j > 0)
                    writer.WriteLine();

                CFormatter.Write(method, writer, remap2);
                writer.WriteLine();
                writer.WriteLine('{');
                writer.Indent++;
                writer.WriteLine("IMPLEMENT_STUB();");

                if (method.Signature.ReturnType is not NamedTypeNode { Name: "void" })
                    writer.WriteLine("return {};");

                writer.Indent--;
                writer.WriteLine('}');
            }
        }
    }

    private void GenerateFactory(IndentedTextWriter writer, string name)
    {
        writer.WriteLine("template<template<abi_t> typename T>");
        writer.WriteLine($"inline HRESULT {name}(abi_t ABI, void **ppvObject)");
        writer.WriteLine('{');
        writer.Indent++;
        writer.WriteLine("if (ppvObject == nullptr)");
        writer.Indent++;
        writer.WriteLine("return E_POINTER;");
        writer.Indent--;
        writer.WriteLine();
        var abis = _versions.OrderDescending().ToArray();

        for (int i = 0; i < abis.Length; i++)
        {
            var abi = $"abi_t{{{abis[i].Major},{abis[i].Minor},{abis[i].Build},{abis[i].Revision}}}";

            if (i > 0)
                writer.Write("else ");

            writer.WriteLine($"if (ABI >= {abi})");
            writer.Indent++;
            writer.WriteLine($"*ppvObject = new T<{abi}>();");
            writer.Indent--;
        }

        writer.WriteLine("else");
        writer.Indent++;
        writer.WriteLine("*ppvObject = new T<abi_t{}>();");
        writer.Indent--;
        writer.WriteLine();
        writer.WriteLine("return S_OK;");
        writer.Indent--;
        writer.WriteLine('}');
    }

    private static void WriteEnumDeclaration(EnumNode node, IndentedTextWriter writer)
    {
        writer.Write(node.DeclarationType);
        writer.Write(' ');
        writer.Write(node.Name);

        if (node.BaseType != null)
        {
            writer.Write(" : ");
            writer.Write(node.BaseType.Name);
        }

        writer.WriteLine();
        writer.WriteLine('{');
        writer.Indent++;

        foreach (var member in node.EnumMembers)
        {
            writer.Write(member.Name);

            if (member.Value != null)
            {
                writer.Write(" = ");
                writer.Write(member.Value.Evaluate());
            }

            writer.WriteLine(',');
        }

        writer.Indent--;
        writer.WriteLine("};");
    }

    private static void WriteRequiresClause(IReadOnlyList<Version> abis, int index, IndentedTextWriter writer)
    {
        if (index < 0 || index >= abis.Count)
            return;

        var thisABi = abis[index];
        var nextAbi = index + 1 < abis.Count ? abis[index + 1] : null;

        writer.Write("requires (ABI >= ");
        WriteAbiVersion(thisABi, writer);

        if (nextAbi != null)
        {
            writer.Write(" && ABI < ");
            WriteAbiVersion(nextAbi, writer);
        }

        writer.WriteLine(')');
    }

    private void WriteConditionalBases(TypeDeclarationNode node, IndentedTextWriter writer)
    {
        var baseAbi = GetBaseTypeAbiVersions(node);

        for (int i = -1; i < baseAbi.Length; i++)
        {
            if (i >= 0)
                writer.WriteLine();

            var abi = i >= 0 ? baseAbi[i] : null;
            WriteConditionalBase(string.Empty);
            writer.WriteLine();
            WriteConditionalBase("Vtbl");

            void WriteConditionalBase(string suffix)
            {
                writer.WriteLine("template<abi_t ABI>");
                WriteRequiresClause(baseAbi, i, writer);
                writer.Write("struct ");
                writer.Write(node.Name);
                writer.Write(suffix);
                writer.Write("Base");

                if (i >= 0)
                    writer.Write("<ABI>");

                foreach (var (j, baseType) in node.BaseTypes.Where(node => node.Exists(abi)).Index())
                {
                    if (j > 0)
                        writer.Write(", ");
                    else
                        writer.Write(" : ");

                    var name = baseType.Name + suffix;
                    writer.Write(_remap.GetValueOrDefault(name, name));
                }

                writer.WriteLine(" {};");
            }
        }
    }

    private static void HandleConditionalAttribute(IAttributableNode node, IndentedTextWriter writer, bool end)
    {
        var indent = writer.Indent;

        if (!end && node.TryGetAttribute(out ConditionalAttribute? conditional))
        {
            writer.Indent = indent - 1;
            writer.WriteLine($"#if {conditional.Condition}");
        }

        if ((end && node.TryGetAttribute<ConditionalAttribute>(out _)) ||
            (node is MemberBlockEndNode endNode && endNode.Block.TryGetAttribute<ConditionalAttribute>(out _)))
        {
            writer.Indent = indent - 1;
            writer.WriteLine("#endif");
        }

        writer.Indent = indent;
    }

    private void WriteDeclaration(TypeDeclarationNode node, IndentedTextWriter writer)
    {
        if (node is EnumNode enumNode)
        {
            WriteEnumDeclaration(enumNode, writer);
            return;
        }

        var isAbiType = _abiTypes.Contains(node.Name!);
        var dataAbi = GetDataAbiVersions(node);
        var codeAbi = GetCodeAbiVersions(node);
        var mainAbi = node is InterfaceNode ? codeAbi : dataAbi;
        bool hasConditionalBases = HasConditionalBases(node);
        bool hasDataMembers = node.EnumerateAllMembers().OfType<FieldNode>().Any();

        if (hasConditionalBases || (node is InterfaceNode && hasDataMembers))
        {
            writer.WriteLine("namespace details");
            writer.WriteLine('{');
            writer.Indent++;
        }

        if (node is InterfaceNode && hasDataMembers)
        {
            for (int i = -1; i < dataAbi.Length; i++)
            {
                if (i >= 0)
                    writer.WriteLine();

                var thisAbi = i >= 0 ? dataAbi[i] : null;
                var nextAbi = i + 1 < dataAbi.Length ? dataAbi[i + 1] : null;

                if (isAbiType)
                {
                    writer.WriteLine("template<abi_t ABI>");
                    WriteRequiresClause(dataAbi, i, writer);
                }

                writer.Write("struct");
                writer.Write(' ');
                writer.Write(node.Name);
                writer.Write("Data");

                if (i >= 0 && isAbiType)
                    writer.Write("<ABI>");

                writer.WriteLine();
                writer.WriteLine('{');
                writer.Indent++;

                foreach (var member in node.EnumerateMembers(thisAbi, includeBlocks: true))
                {
                    if (member is MemberBlockNode or MemberBlockEndNode)
                        HandleConditionalAttribute(member, writer, end: false);

                    if (member is not FieldNode)
                        continue;

                    HandleConditionalAttribute(member, writer, end: false);
                    CFormatter.Write(member, writer, _remap);
                    writer.WriteLine(';');
                    HandleConditionalAttribute(member, writer, end: true);
                }

                writer.Indent--;
                writer.WriteLine("};");
            }
        }

        if (hasConditionalBases)
        {
            if (node is InterfaceNode && hasDataMembers)
                writer.WriteLine();

            WriteConditionalBases(node, writer);
        }

        if (hasConditionalBases || (node is InterfaceNode && hasDataMembers))
        {
            writer.Indent--;
            writer.WriteLine('}');
            writer.WriteLine();
        }

        for (int i = -1; i < mainAbi.Length; i++)
        {
            if (i >= 0)
                writer.WriteLine();

            var thisAbi = i >= 0 ? mainAbi[i] : null;
            var nextAbi = i + 1 < mainAbi.Length ? mainAbi[i + 1] : null;

            if (isAbiType)
            {
                writer.WriteLine("template<abi_t ABI>");
                WriteRequiresClause(mainAbi, i, writer);
            }

            if (node is InterfaceNode)
                writer.Write("struct");
            else
                writer.Write(node.DeclarationType);

            writer.Write(' ');
            writer.Write(node.Name);
            var baseTypes = node.BaseTypes.Where(node => node.Exists(thisAbi));

            if (i >= 0 && isAbiType)
                writer.Write("<ABI>");

            if (hasConditionalBases || baseTypes.Any())
                writer.Write(" : ");

            if (hasConditionalBases)
            {
                if (node is ClassNode)
                    writer.Write("public ");

                writer.Write("details::");
                writer.Write(node.Name);
                writer.Write("Base");
                writer.Write("<ABI>");
            }
            else
            {
                foreach (var (j, baseType) in baseTypes.Index())
                {
                    if (j > 0)
                        writer.Write(", ");

                    if (node is ClassNode)
                        writer.Write("public ");

                    writer.Write(_remap.GetValueOrDefault(baseType.Name, baseType.Name));
                }
            }

            if (node is InterfaceNode && hasDataMembers)
            {
                if (baseTypes.Any())
                    writer.Write(", ");

                writer.Write("details::");
                writer.Write(node.Name);
                writer.Write("Data");

                if (isAbiType)
                {
                    writer.Write("<ABI>");
                }
            }

            writer.WriteLine();
            writer.WriteLine('{');
            writer.Indent++;

            if (node is InterfaceNode)
            {
                foreach (var member in node.EnumerateMembers(thisAbi, includeBlocks: true))
                {
                    if (member is MemberBlockNode or MemberBlockEndNode)
                        HandleConditionalAttribute(member, writer, end: false);

                    if (member is not MethodNode)
                        continue;

                    HandleConditionalAttribute(member, writer, end: false);
                    writer.Write("virtual ");
                    CFormatter.Write(member, writer, _remap);
                    writer.WriteLine(" = 0;");
                    HandleConditionalAttribute(member, writer, end: true);
                }
            }
            else
            {
                foreach (var member in node.EnumerateMembers(thisAbi, includeBlocks: true))
                {
                    if (member is MemberBlockNode or MemberBlockEndNode)
                        HandleConditionalAttribute(member, writer, end: false);

                    if (member is not FieldNode)
                        continue;

                    HandleConditionalAttribute(member, writer, end: false);
                    CFormatter.Write(member, writer, _remap);
                    writer.WriteLine(';');
                    HandleConditionalAttribute(member, writer, end: true);
                }
            }

            writer.Indent--;
            writer.WriteLine("};");
        }

        if (node is InterfaceNode)
        {
            writer.WriteLine();
            WriteVTable(node, writer);
        }
    }

    private void WriteVTable(TypeDeclarationNode node, IndentedTextWriter writer)
    {
        var isAbiType = _abiTypes.Contains(node.Name!);
        var methodAbi = GetCodeAbiVersions(node);
        var thisParam = new ParameterNode([], new PointerTypeNode(new NamedTypeNode("void")), null);
        bool hasConditionalBases = HasConditionalBases(node);

        for (int i = -1; i < methodAbi.Length; i++)
        {
            if (i >= 0)
                writer.WriteLine();

            var thisAbi = i >= 0 ? methodAbi[i] : null;
            var nextAbi = i + 1 < methodAbi.Length ? methodAbi[i + 1] : null;
            var methods = node.EnumerateMembers(thisAbi).OfType<MethodNode>();

            if (isAbiType)
            {
                writer.WriteLine("template<abi_t ABI>");
                WriteRequiresClause(methodAbi, i, writer);
            }

            writer.Write("struct ");
            writer.Write(node.Name);
            writer.Write("Vtbl");

            if (i >= 0 && isAbiType)
                writer.Write("<ABI>");

            if (hasConditionalBases)
            {
                writer.Write(" : ");
                writer.Write("details::");
                writer.Write(node.Name);
                writer.Write("VtblBase");
                writer.Write("<ABI>");
            }
            else
            {
                foreach (var (j, baseType) in node.BaseTypes.Where(node => node.Exists(thisAbi)).Index())
                {
                    if (j > 0)
                        writer.Write(", ");
                    else
                        writer.Write(" : ");

                    var name = baseType.Name + "Vtbl";
                    writer.Write(_remap.GetValueOrDefault(name, name));
                }
            }

            writer.WriteLine();
            writer.WriteLine('{');
            writer.Indent++;

            foreach (var member in node.EnumerateMembers(thisAbi, includeBlocks: true))
            {
                if (member is MemberBlockNode or MemberBlockEndNode)
                    HandleConditionalAttribute(member, writer, end: false);

                if (member is not MethodNode method)
                    continue;

                HandleConditionalAttribute(member, writer, end: false);
                var signature = method.Signature with { Parameters = method.Signature.Parameters.Insert(0, thisParam) };
                CFormatter.Write(new PointerTypeNode(signature, false), writer, _remap, method.Name);
                writer.WriteLine(';');
                HandleConditionalAttribute(member, writer, end: true);
            }

            writer.Indent--;
            writer.WriteLine("};");
        }
    }

    private static bool HasConditionalBases(TypeDeclarationNode node)
    {
        foreach (var baseType in node.BaseTypes)
        {
            if (baseType.TryGetAttribute<AbiAddedAttribute>(out _) ||
                baseType.TryGetAttribute<AbiRemovedAttribute>(out _))
            {
                return true;
            }
        }

        return false;
    }

    private static Version[] GetDataAbiVersions(SyntaxNode node)
    {
        var versions = new HashSet<Version>();
        node.Accept(new DataAbiVersionCollector(versions));
        return [..versions.Order()];
    }

    private static Version[] GetCodeAbiVersions(SyntaxNode node)
    {
        var versions = new HashSet<Version>();
        node.Accept(new CodeAbiVersionCollector(versions));
        return [.. versions.Order()];
    }

    private static Version[] GetBaseTypeAbiVersions(SyntaxNode node)
    {
        var versions = new HashSet<Version>();
        node.Accept(new BaseTypeAbiVersionCollector(versions));
        return [.. versions.Order()];
    }

    private static void WriteAbiVersion(Version version, TextWriter writer)
    {
        writer.Write($"abi_t{{{version.Major},{version.Minor},{version.Build},{version.Revision}}}");
    }

    private static string? GetQualifiedName(TypeDeclarationNode node)
    {
        if (string.IsNullOrEmpty(node.Namespace) || string.IsNullOrEmpty(node.Name))
            return node.Name;

        return node.Namespace.Replace(".", "::") + "::" + node.Name;
    }

    private sealed class DataAbiVersionCollector(ICollection<Version> versions) : SyntaxVisitor
    {
        public override void Visit(AttributeNode node)
        {
            base.Visit(node);

            if (node.TryGetAttribute(out AbiAddedAttribute? added))
            {
                versions.Add(added.Version);
            }

            if (node.TryGetAttribute(out AbiRemovedAttribute? removed))
            {
                versions.Add(removed.Version);
            }
        }

        public override void Visit(MemberBlockNode node)
        {
            var counter = new MemberCounter();
            node.Accept(counter);

            if (counter.FieldCount == 0)
                return;

            base.Visit(node);
        }

        public override void Visit(BaseTypeNode node)
        {
        }

        public override void Visit(MethodNode node)
        {
        }
    }

    private sealed class CodeAbiVersionCollector(ICollection<Version> versions) : SyntaxVisitor
    {
        public override void Visit(AttributeNode node)
        {
            base.Visit(node);

            if (node.TryGetAttribute(out AbiAddedAttribute? added))
            {
                versions.Add(added.Version);
            }

            if (node.TryGetAttribute(out AbiRemovedAttribute? removed))
            {
                versions.Add(removed.Version);
            }
        }

        public override void Visit(MemberBlockNode node)
        {
            var counter = new MemberCounter();
            node.Accept(counter);

            if (counter.MethodCount == 0)
                return;

            base.Visit(node);
        }

        public override void Visit(BaseTypeNode node)
        {
        }

        public override void Visit(FieldNode node)
        {
        }
    }

    private sealed class BaseTypeAbiVersionCollector(ICollection<Version> versions) : SyntaxVisitor
    {
        public override void Visit(AttributeNode node)
        {
            base.Visit(node);

            if (node.TryGetAttribute(out AbiAddedAttribute? added))
            {
                versions.Add(added.Version);
            }

            if (node.TryGetAttribute(out AbiRemovedAttribute? removed))
            {
                versions.Add(removed.Version);
            }
        }

        public override void Visit(MemberBlockNode node)
        {
        }

        public override void Visit(FieldNode node)
        {
        }

        public override void Visit(MethodNode node)
        {
        }
    }

    private sealed class MemberCounter : SyntaxVisitor
    {
        public int MethodCount { get; private set; }

        public int FieldCount { get; private set; }

        public override void Visit(FieldNode node)
        {
            FieldCount++;
            base.Visit(node);
        }

        public override void Visit(MethodNode node)
        {
            MethodCount++;
            base.Visit(node);
        }
    }

    private sealed class AbiVersionVisitor(Dictionary<string, TypeDeclarationNode> types, ICollection<Version> versions) : SyntaxVisitor
    {
        private HashSet<string> _visited = [];

        public override void Visit(AttributeNode node)
        {
            base.Visit(node);

            if (node.TryGetAttribute(out AbiAddedAttribute? added))
            {
                versions.Add(added.Version);
            }

            if (node.TryGetAttribute(out AbiRemovedAttribute? removed))
            {
                versions.Add(removed.Version);
            }
        }

        public override void Visit(BaseTypeNode node)
        {
            if (types.TryGetValue(node.Name, out var baseType) && !_visited.Contains(node.Name))
            {
                _visited.Add(node.Name);
                baseType.Accept(this);
            }

            base.Visit(node);
        }

        public override void Visit(NamedTypeNode node)
        {
            if (types.TryGetValue(node.Name, out var type) && type != node && !_visited.Contains(node.Name))
            {
                _visited.Add(node.Name);
                type.Accept(this);
            }

            base.Visit(node);
        }
    }

    private sealed class HasAbiTypesVisitor(ISet<string> abiTypes, StrongBox<bool> result) : SyntaxVisitor
    {
        public override void Visit(BaseTypeNode node)
        {
            if (abiTypes.Contains(node.Name))
            {
                result.Value = true;
            }
        }

        public override void Visit(NamedTypeNode node)
        {
            if (abiTypes.Contains(node.Name))
            {
                result.Value = true;
            }
        }
    }
}
