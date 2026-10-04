// Builds the Unity API index used by the GhumanteUnityApiAudit analyzer.
//
//   dotnet run --project Indexer.csproj -- <index.tsv> <source-root> [<source-root> ...]
//
// It parses (syntax only, nothing is compiled or executed) every .cs file under the roots, and writes one
// line per declared type and member with its accessibility and [Obsolete] state:
//
//   T <tab> typeKey <tab> public(0|1) <tab> obsolete(0 none|1 warning|2 error) <tab> base simple names (,)
//   M <tab> typeKey <tab> kind <tab> name <tab> public(0|1) <tab> obsolete <tab> type <tab> param types (,) <tab> accessors
//
// typeKey is "Namespace.Outer.Inner" with a `N suffix for generic arity, matching what the analyzer
// computes from compiled symbols. Types are reduced to simple names ("System.Collections.Generic.List<T>"
// -> "List", "int[]" -> "int[]"), which is precise enough to tell overloads apart.
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: Indexer <index.tsv> <source-root> [<source-root> ...]");
    return 2;
}

var parseOptions = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.None, SourceCodeKind.Regular,
    new[] { "UNITY_EDITOR", "ENABLE_UNITYEVENTS", "UNITY_ASSERTIONS", "ENABLE_PROFILER", "UNITY_64",
            "ENABLE_UNITY_COLLECTIONS_CHECKS", "ENABLE_MONO", "ENABLE_IL2CPP" });
var lines = new List<string>();
int files = 0;
foreach (string root in args.Skip(1))
{
    foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
    {
        string norm = path.Replace('\\', '/');
        if (norm.Contains("/Tests/") || norm.Contains("/Test/") || norm.Contains("/Samples~/")) continue;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), parseOptions, path);
        new Walker(lines).Visit(tree.GetRoot());
        files++;
    }
}
lines.Sort(StringComparer.Ordinal);
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
File.WriteAllLines(args[0], lines.Distinct(), new UTF8Encoding(false));
Console.WriteLine($"indexed {files} files, {lines.Count} declarations -> {args[0]}");
return 0;

internal sealed class Walker : CSharpSyntaxWalker
{
    private readonly List<string> _out;
    private readonly Stack<string> _namespaces = new();
    private readonly Stack<(string key, bool isPublic, bool isInterface, bool isEnum)> _types = new();

    /// <summary>"using UnityObject = UnityEngine.Object;" aliases of the file being walked.</summary>
    [ThreadStatic] private static Dictionary<string, TypeSyntax>? s_aliases;

    public Walker(List<string> output)
    {
        _out = output;
    }

    public override void VisitCompilationUnit(CompilationUnitSyntax node)
    {
        s_aliases = new Dictionary<string, TypeSyntax>(StringComparer.Ordinal);
        foreach (UsingDirectiveSyntax u in node.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            if (u.Alias != null && u.NamespaceOrType is TypeSyntax target)
            {
                s_aliases[u.Alias.Name.Identifier.Text] = target;
            }
        }
        base.VisitCompilationUnit(node);
        s_aliases = null;
    }

    public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
    {
        _namespaces.Push(node.Name.ToString());
        base.VisitNamespaceDeclaration(node);
        _namespaces.Pop();
    }

    public override void VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
        _namespaces.Push(node.Name.ToString());
        base.VisitFileScopedNamespaceDeclaration(node);
    }

    private string CurrentNamespace()
    {
        return string.Join(".", _namespaces.Reverse());
    }

    private void EnterType(BaseTypeDeclarationSyntax node, string name, int arity, bool isInterface, bool isEnum,
        Action visitChildren)
    {
        string simple = arity > 0 ? $"{name}`{arity}" : name;
        string key = _types.Count > 0 ? _types.Peek().key + "." + simple
            : (CurrentNamespace().Length > 0 ? CurrentNamespace() + "." + simple : simple);
        // Own modifiers only: a partial declaration may omit "public" that another part declares, so the
        // analyzer ORs all parts together (and checks enclosing types separately).
        bool isPublic = IsAccessible(node.Modifiers, _types.Count > 0 && _types.Peek().isInterface);
        string bases = node.BaseList == null ? "" : string.Join(",", node.BaseList.Types.Select(t => Simple(t.Type)));
        _out.Add(string.Join("\t", "T", key, isPublic ? "1" : "0", Obsolete(node.AttributeLists), bases));
        _types.Push((key, isPublic, isInterface, isEnum));
        visitChildren();
        _types.Pop();
    }

    public override void VisitClassDeclaration(ClassDeclarationSyntax node) =>
        EnterType(node, node.Identifier.Text, node.TypeParameterList?.Parameters.Count ?? 0, false, false, () => base.VisitClassDeclaration(node));

    public override void VisitStructDeclaration(StructDeclarationSyntax node) =>
        EnterType(node, node.Identifier.Text, node.TypeParameterList?.Parameters.Count ?? 0, false, false, () => base.VisitStructDeclaration(node));

    public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) =>
        EnterType(node, node.Identifier.Text, node.TypeParameterList?.Parameters.Count ?? 0, true, false, () => base.VisitInterfaceDeclaration(node));

    public override void VisitRecordDeclaration(RecordDeclarationSyntax node) =>
        EnterType(node, node.Identifier.Text, node.TypeParameterList?.Parameters.Count ?? 0, false, false, () => base.VisitRecordDeclaration(node));

    public override void VisitEnumDeclaration(EnumDeclarationSyntax node) =>
        EnterType(node, node.Identifier.Text, 0, false, true, () => base.VisitEnumDeclaration(node));

    public override void VisitDelegateDeclaration(DelegateDeclarationSyntax node)
    {
        int arity = node.TypeParameterList?.Parameters.Count ?? 0;
        string simple = arity > 0 ? $"{node.Identifier.Text}`{arity}" : node.Identifier.Text;
        string key = _types.Count > 0 ? _types.Peek().key + "." + simple
            : (CurrentNamespace().Length > 0 ? CurrentNamespace() + "." + simple : simple);
        bool isPublic = IsAccessible(node.Modifiers, _types.Count > 0 && _types.Peek().isInterface);
        _out.Add(string.Join("\t", "T", key, isPublic ? "1" : "0", Obsolete(node.AttributeLists), "Delegate"));
    }

    public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
    {
        if (_types.Count == 0) return;
        AddMember("enummember", node.Identifier.Text, true, node.AttributeLists, _types.Peek().key.Split('.').Last(), "");
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (_types.Count == 0 || node.ExplicitInterfaceSpecifier != null) return;
        AddMember("method", node.Identifier.Text, MemberAccessible(node.Modifiers), node.AttributeLists,
            Simple(node.ReturnType), Params(node.ParameterList));
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        if (_types.Count == 0 || node.Modifiers.Any(SyntaxKind.StaticKeyword)) return;
        AddMember("ctor", ".ctor", MemberAccessible(node.Modifiers), node.AttributeLists, "void", Params(node.ParameterList));
    }

    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        if (_types.Count == 0 || node.ExplicitInterfaceSpecifier != null) return;
        AddMember("property", node.Identifier.Text, MemberAccessible(node.Modifiers), node.AttributeLists, Simple(node.Type), "",
            Accessors(node.AccessorList, node.ExpressionBody != null));
    }

    public override void VisitIndexerDeclaration(IndexerDeclarationSyntax node)
    {
        if (_types.Count == 0 || node.ExplicitInterfaceSpecifier != null) return;
        AddMember("property", "this[]", MemberAccessible(node.Modifiers), node.AttributeLists, Simple(node.Type), Params(node.ParameterList),
            Accessors(node.AccessorList, node.ExpressionBody != null));
    }

    public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
    {
        if (_types.Count == 0) return;
        foreach (VariableDeclaratorSyntax v in node.Declaration.Variables)
        {
            AddMember("field", v.Identifier.Text, MemberAccessible(node.Modifiers), node.AttributeLists, Simple(node.Declaration.Type), "");
        }
    }

    public override void VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
    {
        if (_types.Count == 0) return;
        foreach (VariableDeclaratorSyntax v in node.Declaration.Variables)
        {
            AddMember("event", v.Identifier.Text, MemberAccessible(node.Modifiers), node.AttributeLists, Simple(node.Declaration.Type), "");
        }
    }

    public override void VisitEventDeclaration(EventDeclarationSyntax node)
    {
        if (_types.Count == 0 || node.ExplicitInterfaceSpecifier != null) return;
        AddMember("event", node.Identifier.Text, MemberAccessible(node.Modifiers), node.AttributeLists, Simple(node.Type), "");
    }

    private void AddMember(string kind, string name, bool accessible, SyntaxList<AttributeListSyntax> attrs, string type,
        string parameters, string accessors = "")
    {
        var t = _types.Peek();
        bool isPublic = accessible;
        _out.Add(string.Join("\t", "M", t.key, kind, name, isPublic ? "1" : "0", Obsolete(attrs), type, parameters, accessors));
    }

    /// <summary>"get,set", "get" or "get,set-restricted" (setter private/internal).</summary>
    private static string Accessors(AccessorListSyntax? list, bool expressionBodied)
    {
        if (expressionBodied || list == null) return "get";
        var parts = new List<string>();
        foreach (AccessorDeclarationSyntax a in list.Accessors)
        {
            string kind = a.Keyword.Text;
            if (kind is not ("get" or "set" or "init")) continue;
            bool restricted = a.Modifiers.Any(SyntaxKind.PrivateKeyword) ||
                              (a.Modifiers.Any(SyntaxKind.InternalKeyword) && !a.Modifiers.Any(SyntaxKind.ProtectedKeyword));
            parts.Add(restricted ? kind + "-restricted" : kind);
        }
        return string.Join(",", parts);
    }

    private bool MemberAccessible(SyntaxTokenList modifiers)
    {
        var t = _types.Peek();
        if (t.isEnum) return true;
        return IsAccessible(modifiers, t.isInterface);
    }

    private static bool IsAccessible(SyntaxTokenList modifiers, bool inInterface)
    {
        bool isPublic = modifiers.Any(SyntaxKind.PublicKeyword);
        bool isProtected = modifiers.Any(SyntaxKind.ProtectedKeyword) && !modifiers.Any(SyntaxKind.PrivateKeyword);
        bool hasAccess = modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword) || m.IsKind(SyntaxKind.PrivateKeyword) ||
                                            m.IsKind(SyntaxKind.InternalKeyword) || m.IsKind(SyntaxKind.ProtectedKeyword));
        return isPublic || isProtected || (inInterface && !hasAccess);
    }

    private static string Params(BaseParameterListSyntax list)
    {
        return string.Join(",", list.Parameters.Select(p => p.Type == null ? "?" : Simple(p.Type)));
    }

    /// <summary>"global::System.Collections.Generic.List&lt;int&gt;[]" -> "List[]"; "int?" -> "Nullable"; "T" stays "T".</summary>
    public static string Simple(TypeSyntax type)
    {
        switch (type)
        {
            case ArrayTypeSyntax a:
                return Simple(a.ElementType) + string.Concat(a.RankSpecifiers.Select(r => "[" + new string(',', r.Rank - 1) + "]"));
            case NullableTypeSyntax:
                return "Nullable";
            case PointerTypeSyntax p:
                return Simple(p.ElementType) + "*";
            case RefTypeSyntax r:
                return Simple(r.Type);
            case QualifiedNameSyntax q:
                return Simple(q.Right);
            case AliasQualifiedNameSyntax aq:
                return Simple(aq.Name);
            case GenericNameSyntax g:
                return g.Identifier.Text;
            case IdentifierNameSyntax i:
                // Resolve one alias level only (aliases like "using Object = UnityEngine.Object;" would loop).
                return s_aliases != null && s_aliases.TryGetValue(i.Identifier.Text, out TypeSyntax? target)
                    ? LastIdentifier(target)
                    : i.Identifier.Text;
            case PredefinedTypeSyntax pd:
                return pd.Keyword.Text;
            case TupleTypeSyntax:
                return "ValueTuple";
            default:
                return type.ToString();
        }
    }

    private static string LastIdentifier(TypeSyntax type)
    {
        string text = type.ToString();
        int generic = text.IndexOf('<');
        if (generic >= 0) text = text.Substring(0, generic);
        return text.Substring(text.LastIndexOf('.') + 1).Replace("global::", "");
    }

    /// <summary>0 = not obsolete, 1 = [Obsolete] warning, 2 = [Obsolete(..., true)] error.</summary>
    private static string Obsolete(SyntaxList<AttributeListSyntax> lists)
    {
        foreach (AttributeSyntax a in lists.SelectMany(l => l.Attributes))
        {
            string n = a.Name.ToString();
            if (n is not ("Obsolete" or "ObsoleteAttribute" or "System.Obsolete" or "System.ObsoleteAttribute")) continue;
            var argsList = a.ArgumentList?.Arguments;
            if (argsList is { Count: >= 2 } && argsList.Value[1].Expression.IsKind(SyntaxKind.TrueLiteralExpression)) return "2";
            if (argsList != null && argsList.Value.Any(x => x.NameColon?.Name.Identifier.Text == "error" &&
                                                            x.Expression.IsKind(SyntaxKind.TrueLiteralExpression))) return "2";
            return "1";
        }
        return "0";
    }
}
