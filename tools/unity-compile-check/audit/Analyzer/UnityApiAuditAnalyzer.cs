using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Ghumante.UnityApiAudit
{
    /// <summary>
    /// Checks every Unity type and member our code binds to against an index of Unity's real 6000.3 API
    /// (built by audit/Indexer from the Unity C# reference source and the Graphics repo). The compile check
    /// builds against older reference assemblies (2021.3) and hand-written stubs; this analyzer is what
    /// proves the code also fits Unity 6.3: the member exists there, is public, has the same signature,
    /// is writable when we write it, and is not [Obsolete].
    /// The index arrives as an AdditionalFiles item named unity-api-index.tsv; without it the analyzer is off.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnityApiAuditAnalyzer : DiagnosticAnalyzer
    {
        private const string Category = "UnityApi";
        private const string IndexFileName = "unity-api-index.tsv";

        public static readonly DiagnosticDescriptor Missing = new DiagnosticDescriptor(
            "GHU001", "Unity API not found", "'{0}' does not exist in the Unity 6.3 API (reference source)",
            Category, DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor Obsolete = new DiagnosticDescriptor(
            "GHU002", "Unity API obsolete", "'{0}' is [Obsolete] in Unity 6.3{1}",
            Category, DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor Signature = new DiagnosticDescriptor(
            "GHU003", "Unity API signature differs", "'{0}' has a different signature in Unity 6.3: {1}",
            Category, DiagnosticSeverity.Error, true);

        public static readonly DiagnosticDescriptor NotPublic = new DiagnosticDescriptor(
            "GHU004", "Unity API not public", "'{0}' is not public in Unity 6.3{1}",
            Category, DiagnosticSeverity.Error, true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Missing, Obsolete, Signature, NotPublic);

        private static readonly ConcurrentDictionary<string, ApiIndex> Cache = new ConcurrentDictionary<string, ApiIndex>();

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(start =>
            {
                AdditionalText file = start.Options.AdditionalFiles.FirstOrDefault(
                    f => f.Path.EndsWith(IndexFileName, StringComparison.OrdinalIgnoreCase));
                SourceText text = file?.GetText(start.CancellationToken);
                if (text == null) return;
                // One parse per compiler server process and index file version.
                string cacheKey = file.Path + "|" + text.Length + "|" + text.Lines.Count;
                ApiIndex index = Cache.GetOrAdd(cacheKey, _ => ApiIndex.Parse(text));
                var seen = new ConcurrentDictionary<(ISymbol, Location), bool>();
                start.RegisterSyntaxNodeAction(
                    ctx => Check(ctx, index, seen),
                    SyntaxKind.IdentifierName, SyntaxKind.GenericName, SyntaxKind.ObjectCreationExpression,
                    SyntaxKind.ImplicitObjectCreationExpression, SyntaxKind.Attribute);
            });
        }

        private static void Check(SyntaxNodeAnalysisContext ctx, ApiIndex index, ConcurrentDictionary<(ISymbol, Location), bool> seen)
        {
            SymbolInfo info = ctx.SemanticModel.GetSymbolInfo(ctx.Node, ctx.CancellationToken);
            ISymbol symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
            if (symbol == null || !IsUnity(symbol)) return;
            if (symbol is IMethodSymbol m && m.ReducedFrom != null) symbol = m.ReducedFrom;
            symbol = symbol.OriginalDefinition;
            Location location = ctx.Node.GetLocation();
            if (!seen.TryAdd((symbol, location), true)) return;

            switch (symbol)
            {
                case INamedTypeSymbol type:
                    CheckType(ctx, index, type, location);
                    break;
                case IMethodSymbol method when method.MethodKind == MethodKind.Ordinary ||
                                               method.MethodKind == MethodKind.Constructor:
                    CheckMember(ctx, index, method, method.MethodKind == MethodKind.Constructor ? "ctor" : "method",
                        method.MethodKind == MethodKind.Constructor ? ".ctor" : method.Name,
                        method.Parameters.Select(p => Simple(p.Type)).ToArray(), null, false, location);
                    break;
                case IPropertySymbol property:
                    CheckMember(ctx, index, property, "property", property.IsIndexer ? "this[]" : property.Name,
                        property.IsIndexer ? property.Parameters.Select(p => Simple(p.Type)).ToArray() : null, Simple(property.Type),
                        IsWritten(ctx.Node), location);
                    break;
                case IFieldSymbol field:
                    CheckMember(ctx, index, field, field.ContainingType.TypeKind == TypeKind.Enum ? "enummember" : "field",
                        field.Name, null, field.ContainingType.TypeKind == TypeKind.Enum ? null : Simple(field.Type), false, location);
                    break;
                case IEventSymbol ev:
                    CheckMember(ctx, index, ev, "event", ev.Name, null, null, false, location);
                    break;
            }
        }

        private static bool IsUnity(ISymbol symbol)
        {
            string assembly = symbol.ContainingAssembly?.Name;
            if (assembly == null) return false;
            return assembly.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                   assembly.StartsWith("UnityEditor", StringComparison.Ordinal) ||
                   assembly.StartsWith("Unity.", StringComparison.Ordinal);
        }

        private static void CheckType(SyntaxNodeAnalysisContext ctx, ApiIndex index, INamedTypeSymbol type, Location location)
        {
            string key = TypeKey(type);
            if (!index.Types.TryGetValue(key, out ApiIndex.TypeEntry entry))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(Missing, location, key));
                return;
            }
            if (!entry.IsPublic)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(NotPublic, location, key, ""));
            }
            else if (type.ContainingType != null && index.Types.TryGetValue(TypeKey(type.ContainingType), out ApiIndex.TypeEntry outer) && !outer.IsPublic)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(NotPublic, location, key, " (its enclosing type is not public)"));
            }
            if (entry.Obsolete > 0)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(Obsolete, location, key, entry.Obsolete == 2 ? " (error)" : ""));
            }
        }

        private static void CheckMember(SyntaxNodeAnalysisContext ctx, ApiIndex index, ISymbol symbol, string kind,
            string name, string[] parameters, string valueType, bool written, Location location)
        {
            string typeKey = TypeKey(symbol.ContainingType);
            string display = typeKey + "." + name;
            List<ApiIndex.MemberEntry> candidates = index.FindMembers(typeKey, name);
            if (candidates.Count == 0 && kind == "ctor" && parameters.Length == 0 && index.Types.ContainsKey(typeKey))
            {
                return; // implicit parameterless constructor (no constructor declared in the source)
            }
            if (candidates.Count == 0)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(Missing, location, display));
                return;
            }

            ApiIndex.MemberEntry match = null;
            foreach (ApiIndex.MemberEntry c in candidates)
            {
                if (KindMatches(kind, c.Kind) && ParametersMatch(parameters, c.Parameters) &&
                    (valueType == null || TypeNamesEqual(valueType, c.Type)))
                {
                    match = c;
                    break;
                }
            }
            if (match == null)
            {
                string ours = parameters != null ? "(" + string.Join(", ", parameters) + ")" : valueType ?? "";
                string theirs = string.Join(" | ", candidates.Select(c => c.Describe()).Distinct().Take(6));
                ctx.ReportDiagnostic(Diagnostic.Create(Signature, location, display + " " + ours, theirs));
                return;
            }
            if (!candidates.Where(c => ReferenceEquals(c, match) || c.SameSignature(match)).Any(c => c.IsPublic))
            {
                ctx.ReportDiagnostic(Diagnostic.Create(NotPublic, location, display, ""));
            }
            else if (written && kind == "property" && !match.HasPublicSetter)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(NotPublic, location, display, " (its setter is not public: " + match.Accessors + ")"));
            }
            int obsolete = candidates.Where(c => c.SameSignature(match)).Select(c => c.Obsolete).DefaultIfEmpty(0).Min();
            if (obsolete > 0)
            {
                ctx.ReportDiagnostic(Diagnostic.Create(Obsolete, location, display, obsolete == 2 ? " (error)" : ""));
            }
        }

        private static bool KindMatches(string ours, string theirs)
        {
            if (ours == theirs) return true;
            // Unity sometimes turns a field into a property or vice versa; reading code is unaffected.
            return (ours == "field" || ours == "property") && (theirs == "field" || theirs == "property");
        }

        private static bool ParametersMatch(string[] ours, string[] theirs)
        {
            if (ours == null) return true;
            if (theirs == null || ours.Length != theirs.Length) return false;
            for (int i = 0; i < ours.Length; i++)
            {
                if (ours[i] == "*" || IsTypeParameterName(theirs[i])) continue;
                if (!TypeNamesEqual(ours[i], theirs[i])) return false;
            }
            return true;
        }

        private static bool IsTypeParameterName(string name)
        {
            return name.Length == 1 && char.IsUpper(name[0]) ||
                   name.Length > 1 && name[0] == 'T' && char.IsUpper(name[1]) && !name.Contains("[");
        }

        private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>
        {
            { "Int32", "int" }, { "Single", "float" }, { "Boolean", "bool" }, { "String", "string" },
            { "Double", "double" }, { "Int64", "long" }, { "UInt32", "uint" }, { "UInt64", "ulong" },
            { "Byte", "byte" }, { "Char", "char" }, { "Void", "void" }, { "Int16", "short" },
            { "UInt16", "ushort" }, { "SByte", "sbyte" }, { "Decimal", "decimal" },
        };

        private static bool TypeNamesEqual(string a, string b)
        {
            return Normalize(a) == Normalize(b);
        }

        private static string Normalize(string name)
        {
            string suffix = "";
            int bracket = name.IndexOf('[');
            if (bracket >= 0)
            {
                suffix = name.Substring(bracket);
                name = name.Substring(0, bracket);
            }
            return (Aliases.TryGetValue(name, out string alias) ? alias : name) + suffix;
        }

        private static bool IsWritten(SyntaxNode node)
        {
            SyntaxNode target = node.Parent is MemberAccessExpressionSyntax ma && ma.Name == node ? ma : node;
            switch (target.Parent)
            {
                case AssignmentExpressionSyntax assignment:
                    return assignment.Left == target;
                case PrefixUnaryExpressionSyntax prefix:
                    return prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression);
                case PostfixUnaryExpressionSyntax _:
                    return true;
                default:
                    return false;
            }
        }

        public static string TypeKey(INamedTypeSymbol type)
        {
            type = type.OriginalDefinition;
            if (type.ContainingType != null) return TypeKey(type.ContainingType) + "." + type.MetadataName;
            string ns = type.ContainingNamespace == null || type.ContainingNamespace.IsGlobalNamespace
                ? ""
                : type.ContainingNamespace.ToDisplayString();
            return ns.Length > 0 ? ns + "." + type.MetadataName : type.MetadataName;
        }

        public static string Simple(ITypeSymbol type)
        {
            switch (type)
            {
                case IArrayTypeSymbol a:
                    return Simple(a.ElementType) + "[" + new string(',', a.Rank - 1) + "]";
                case IPointerTypeSymbol p:
                    return Simple(p.PointedAtType) + "*";
                case ITypeParameterSymbol _:
                    return "*";
                case INamedTypeSymbol n when n.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T:
                    return "Nullable";
                case INamedTypeSymbol n when n.IsTupleType:
                    return "ValueTuple";
                default:
                    switch (type.SpecialType)
                    {
                        case SpecialType.System_Object: return "object";
                        case SpecialType.System_Boolean: return "bool";
                        case SpecialType.System_Char: return "char";
                        case SpecialType.System_SByte: return "sbyte";
                        case SpecialType.System_Byte: return "byte";
                        case SpecialType.System_Int16: return "short";
                        case SpecialType.System_UInt16: return "ushort";
                        case SpecialType.System_Int32: return "int";
                        case SpecialType.System_UInt32: return "uint";
                        case SpecialType.System_Int64: return "long";
                        case SpecialType.System_UInt64: return "ulong";
                        case SpecialType.System_Decimal: return "decimal";
                        case SpecialType.System_Single: return "float";
                        case SpecialType.System_Double: return "double";
                        case SpecialType.System_String: return "string";
                        case SpecialType.System_Void: return "void";
                        default: return type.Name;
                    }
            }
        }
    }

    /// <summary>The parsed unity-api-index.tsv (see audit/Indexer/Program.cs for the format).</summary>
    public sealed class ApiIndex
    {
        public sealed class TypeEntry
        {
            public bool IsPublic;
            public int Obsolete;
            public readonly List<string> Bases = new List<string>();
        }

        public sealed class MemberEntry
        {
            public string Kind;
            public string Name;
            public bool IsPublic;
            public int Obsolete;
            public string Type;
            public string[] Parameters;
            public string Accessors;

            public bool HasPublicSetter => Accessors != null &&
                Accessors.Split(',').Any(a => a == "set" || a == "init");

            public bool SameSignature(MemberEntry other)
            {
                return Kind == other.Kind && Type == other.Type &&
                       string.Join(",", Parameters ?? new string[0]) == string.Join(",", other.Parameters ?? new string[0]);
            }

            public string Describe()
            {
                return Kind == "method" || Kind == "ctor"
                    ? Name + "(" + string.Join(", ", Parameters ?? new string[0]) + ")"
                    : Kind + " " + Type + " " + Name;
            }
        }

        public readonly Dictionary<string, TypeEntry> Types = new Dictionary<string, TypeEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<MemberEntry>> _members = new Dictionary<string, List<MemberEntry>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _keysBySimpleName = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        public static ApiIndex Parse(SourceText text)
        {
            var index = new ApiIndex();
            foreach (TextLine line in text.Lines)
            {
                string[] f = line.ToString().Split('\t');
                if (f.Length >= 4 && f[0] == "T")
                {
                    if (!index.Types.TryGetValue(f[1], out TypeEntry t))
                    {
                        t = new TypeEntry();
                        index.Types[f[1]] = t;
                        string simple = f[1].Substring(f[1].LastIndexOf('.') + 1);
                        if (!index._keysBySimpleName.TryGetValue(simple, out List<string> keys))
                        {
                            index._keysBySimpleName[simple] = keys = new List<string>();
                        }
                        keys.Add(f[1]);
                    }
                    t.IsPublic |= f[2] == "1";
                    t.Obsolete = Math.Max(t.Obsolete, int.Parse(f[3]));
                    if (f.Length > 4 && f[4].Length > 0) t.Bases.AddRange(f[4].Split(','));
                }
                else if (f.Length >= 8 && f[0] == "M")
                {
                    var m = new MemberEntry
                    {
                        Kind = f[2],
                        Name = f[3],
                        IsPublic = f[4] == "1",
                        Obsolete = int.Parse(f[5]),
                        Type = f[6],
                        Parameters = f[2] == "method" || f[2] == "ctor" || f[3] == "this[]"
                            ? (f[7].Length == 0 ? new string[0] : f[7].Split(','))
                            : null,
                        Accessors = f.Length > 8 ? f[8] : null,
                    };
                    string key = f[1] + "::" + f[3];
                    if (!index._members.TryGetValue(key, out List<MemberEntry> list))
                    {
                        index._members[key] = list = new List<MemberEntry>();
                    }
                    list.Add(m);
                }
            }
            return index;
        }

        /// <summary>Members named <paramref name="name"/> on the type or, failing that, on its base types.</summary>
        public List<MemberEntry> FindMembers(string typeKey, string name)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(typeKey);
            while (queue.Count > 0 && visited.Count < 32)
            {
                string key = queue.Dequeue();
                if (!visited.Add(key)) continue;
                if (_members.TryGetValue(key + "::" + name, out List<MemberEntry> found)) return found;
                if (!Types.TryGetValue(key, out TypeEntry t)) continue;
                foreach (string b in t.Bases)
                {
                    if (_keysBySimpleName.TryGetValue(b, out List<string> keys))
                    {
                        foreach (string k in keys) queue.Enqueue(k);
                    }
                }
            }
            return new List<MemberEntry>();
        }
    }
}
