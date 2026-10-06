// Generates Documentation/API from Final Factory's shipped assemblies: the public, mod-facing types and
// members, with C# signatures. Doc-comment summaries come from the game's source when the game team runs it
// with --source (they are then stored in doc-comments.json); everyone else's run reuses that file.
//
// Built and run by Tools/generate-api-reference.sh / .ps1 with the Roslyn compiler and .NET runtime that
// ship inside the Unity editor, so it needs nothing but Unity and a Final Factory install.
//
// Usage:
//   ApiReference --dlls <folder with FFCore.dll ...> --config <api-reference.json> --out <Documentation/API>
//                [--refs <folder>]... [--source <game Assets/Scripts>] [--game-version <x.y.z.w>]

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed class Config
{
    public List<AssemblyRule> Assemblies { get; set; } = new();
    public List<string> ExcludeNamespaces { get; set; } = new();
    public List<string> ExcludeTypes { get; set; } = new();
    public int SummaryMaxChars { get; set; } = 400;
}

internal sealed class AssemblyRule
{
    public string Name { get; set; } = "";
    // Namespaces (prefix match) to include; empty = every namespace not excluded.
    public List<string> IncludeNamespaces { get; set; } = new();
    // When set, only these types (full names) are included from this assembly.
    public List<string> OnlyTypes { get; set; } = new();
}

internal static class Program
{
    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints | SymbolDisplayGenericsOptions.IncludeVariance,
        memberOptions: SymbolDisplayMemberOptions.None,
        kindOptions: SymbolDisplayKindOptions.IncludeTypeKeyword,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private static readonly SymbolDisplayFormat MemberFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
        memberOptions: SymbolDisplayMemberOptions.IncludeAccessibility | SymbolDisplayMemberOptions.IncludeModifiers |
                       SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeParameters |
                       SymbolDisplayMemberOptions.IncludeConstantValue | SymbolDisplayMemberOptions.IncludeRef,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName |
                          SymbolDisplayParameterOptions.IncludeParamsRefOut | SymbolDisplayParameterOptions.IncludeDefaultValue |
                          SymbolDisplayParameterOptions.IncludeExtensionThis,
        propertyStyle: SymbolDisplayPropertyStyle.ShowReadWriteDescriptor,
        kindOptions: SymbolDisplayKindOptions.None,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    // Attributes worth showing to a mod author; the rest are noise.
    private static readonly string[] ShownAttributes =
    {
        "Save", "SaveAttribute", "UpdateInGroup", "UpdateInGroupAttribute", "UpdateBefore", "UpdateBeforeAttribute",
        "UpdateAfter", "UpdateAfterAttribute", "MaterialProperty", "MaterialPropertyAttribute", "Obsolete",
        "ObsoleteAttribute", "InternalBufferCapacity", "InternalBufferCapacityAttribute", "Flags", "FlagsAttribute"
    };

    private static int Main(string[] args)
    {
        var dllDirs = new List<string>();
        var refDirs = new List<string>();
        string configPath = null, outDir = null, sourceDir = null, gameVersion = null;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--dlls": dllDirs.Add(args[++i]); break;
                case "--refs": refDirs.Add(args[++i]); break;
                case "--config": configPath = args[++i]; break;
                case "--out": outDir = args[++i]; break;
                case "--source": sourceDir = args[++i]; break;
                case "--game-version": gameVersion = args[++i]; break;
                default:
                    Console.Error.WriteLine($"Unknown argument {args[i]}");
                    return 2;
            }
        }

        if (dllDirs.Count == 0 || configPath == null || outDir == null)
        {
            Console.Error.WriteLine("Usage: ApiReference --dlls <dir> --config <json> --out <dir> [--refs <dir>]... [--source <dir>] [--game-version <v>]");
            return 2;
        }

        var config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true })!;

        // Every managed DLL in the given folders is a reference, so signatures resolve; the first folder that has
        // a given file name wins (the mod's FinalFactoryDlls before the game's Managed folder).
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in dllDirs.Concat(refDirs))
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.dll"))
            {
                var name = Path.GetFileName(file);
                if (!byName.ContainsKey(name) && IsManaged(file)) byName[name] = file;
            }
        }

        var references = byName.Values.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
        var compilation = CSharpCompilation.Create("ApiReference", references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var docsPath = Path.Combine(outDir, "doc-comments.json");
        Dictionary<string, string> docs;
        if (sourceDir != null)
        {
            docs = DocCommentReader.Read(sourceDir, config.SummaryMaxChars);
            Console.WriteLine($"Read {docs.Count} doc-comment summaries from {sourceDir}");
        }
        else if (File.Exists(docsPath))
        {
            docs = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(docsPath)) ?? new();
            Console.WriteLine($"Reusing {docs.Count} doc-comment summaries from {docsPath}");
        }
        else
        {
            docs = new();
            Console.WriteLine("No doc comments available (no --source, no doc-comments.json): signatures only.");
        }

        var types = new List<INamedTypeSymbol>();
        foreach (var rule in config.Assemblies)
        {
            if (!byName.TryGetValue(rule.Name + ".dll", out var path))
            {
                Console.Error.WriteLine($"Missing {rule.Name}.dll in the --dlls folders");
                return 1;
            }

            var reference = references.First(r => string.Equals(((PortableExecutableReference)r).FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
            {
                Console.Error.WriteLine($"Could not load {path}");
                return 1;
            }

            foreach (var type in AllTypes(assembly.GlobalNamespace))
            {
                if (IsIncluded(type, rule, config)) types.Add(type);
            }
        }

        Directory.CreateDirectory(outDir);
        foreach (var old in Directory.GetFiles(outDir, "*.md").Where(f => !Path.GetFileName(f).Equals("README.md", StringComparison.OrdinalIgnoreCase)))
        {
            File.Delete(old);
        }

        var usedDocs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var index = new StringBuilder();
        var flat = new StringBuilder();
        var byNamespace = types.GroupBy(t => NamespaceOf(t)).OrderBy(g => g.Key, StringComparer.Ordinal);
        foreach (var group in byNamespace)
        {
            var md = new StringBuilder();
            md.AppendLine($"# {group.Key}");
            md.AppendLine();
            md.AppendLine($"Assembly: {string.Join(", ", group.Select(t => t.ContainingAssembly.Name).Distinct())}. Generated by `Tools/generate-api-reference`; do not edit by hand.");
            md.AppendLine();
            foreach (var type in group.OrderBy(t => DisplayName(t), StringComparer.Ordinal))
            {
                WriteType(md, flat, type, docs, usedDocs);
            }

            var fileName = (group.Key.Length == 0 ? "global" : group.Key) + ".md";
            File.WriteAllText(Path.Combine(outDir, fileName), md.ToString().Replace("\r\n", "\n"));
            index.AppendLine($"| [{(group.Key.Length == 0 ? "(global)" : group.Key)}]({fileName}) | {group.Count()} |");
        }

        var header = new StringBuilder();
        header.AppendLine("# API reference index");
        header.AppendLine();
        header.AppendLine($"Generated from the game's assemblies{(gameVersion != null ? $" for Final Factory {gameVersion}" : "")} by `Tools/generate-api-reference`. Do not edit by hand; see `README.md` here for what is included and how to regenerate.");
        header.AppendLine();
        header.AppendLine("| Namespace | Types |");
        header.AppendLine("|---|---|");
        File.WriteAllText(Path.Combine(outDir, "INDEX.md"), (header + index.ToString()).Replace("\r\n", "\n"));
        File.WriteAllText(Path.Combine(outDir, "all-members.txt"), flat.ToString().Replace("\r\n", "\n"));
        if (sourceDir != null)
        {
            File.WriteAllText(docsPath, JsonSerializer.Serialize(usedDocs, new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n");
        }

        Console.WriteLine($"Wrote {types.Count} types in {byNamespace.Count()} namespaces to {outDir}");
        return 0;
    }

    private static bool IsManaged(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            return pe.HasMetadata;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            foreach (var t in WithNested(type)) yield return t;
        }

        foreach (var child in ns.GetNamespaceMembers())
        {
            foreach (var t in AllTypes(child)) yield return t;
        }
    }

    private static IEnumerable<INamedTypeSymbol> WithNested(INamedTypeSymbol type)
    {
        yield return type;
        foreach (var nested in type.GetTypeMembers())
        {
            foreach (var t in WithNested(nested)) yield return t;
        }
    }

    private static string NamespaceOf(INamedTypeSymbol type) =>
        type.ContainingNamespace == null || type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString();

    private static string FullName(INamedTypeSymbol type)
    {
        var ns = NamespaceOf(type);
        var name = DisplayName(type);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    private static string DisplayName(INamedTypeSymbol type)
    {
        var parts = new List<string>();
        for (var t = type; t != null; t = t.ContainingType) parts.Insert(0, t.Name);
        return string.Join(".", parts);
    }

    private static bool IsPubliclyVisible(INamedTypeSymbol type)
    {
        for (var t = type; t != null; t = t.ContainingType)
        {
            if (t.DeclaredAccessibility != Accessibility.Public) return false;
        }

        return true;
    }

    private static bool IsIncluded(INamedTypeSymbol type, AssemblyRule rule, Config config)
    {
        if (!IsPubliclyVisible(type) || type.IsImplicitlyDeclared) return false;
        // Compiler- and source-generator-made types (lambdas, job wrappers, "__codegen__" helpers).
        if (type.Name.Contains("<") || type.Name.Contains("__") || type.Name.StartsWith("<", StringComparison.Ordinal)) return false;
        var full = FullName(type);
        var ns = NamespaceOf(type);
        if (rule.OnlyTypes.Count > 0) return rule.OnlyTypes.Contains(full);
        if (config.ExcludeTypes.Contains(full)) return false;
        if (config.ExcludeNamespaces.Any(p => ns == p || ns.StartsWith(p + ".", StringComparison.Ordinal))) return false;
        if (rule.IncludeNamespaces.Count == 0) return true;
        return rule.IncludeNamespaces.Any(p => p == "" ? ns.Length == 0 : ns == p || ns.StartsWith(p + ".", StringComparison.Ordinal));
    }

    private static string Attributes(ISymbol symbol)
    {
        var shown = symbol.GetAttributes()
            .Where(a => a.AttributeClass != null && ShownAttributes.Contains(a.AttributeClass.Name))
            .Select(a =>
            {
                var name = a.AttributeClass!.Name.EndsWith("Attribute", StringComparison.Ordinal)
                    ? a.AttributeClass.Name.Substring(0, a.AttributeClass.Name.Length - "Attribute".Length)
                    : a.AttributeClass.Name;
                var args = a.ConstructorArguments.Select(FormatConstant)
                    .Concat(a.NamedArguments.Select(n => $"{n.Key} = {FormatConstant(n.Value)}")).ToList();
                return args.Count == 0 ? $"[{name}]" : $"[{name}({string.Join(", ", args)})]";
            }).ToList();
        return shown.Count == 0 ? "" : string.Join(" ", shown);
    }

    private static string FormatConstant(TypedConstant c)
    {
        if (c.Kind == TypedConstantKind.Type && c.Value is ITypeSymbol t) return $"typeof({t.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)})";
        if (c.Kind == TypedConstantKind.Array) return "[" + string.Join(", ", c.Values.Select(FormatConstant)) + "]";
        return c.ToCSharpString();
    }

    private static string TypeHeader(INamedTypeSymbol type)
    {
        var sb = new StringBuilder();
        var attrs = Attributes(type);
        if (attrs.Length > 0) sb.AppendLine(attrs);
        sb.Append("public ");
        if (type.TypeKind == TypeKind.Class)
        {
            if (type.IsStatic) sb.Append("static ");
            else if (type.IsAbstract) sb.Append("abstract ");
            else if (type.IsSealed) sb.Append("sealed ");
        }

        sb.Append(type.ToDisplayString(TypeFormat));
        var bases = new List<string>();
        if (type.TypeKind == TypeKind.Class && type.BaseType != null && type.BaseType.SpecialType != SpecialType.System_Object)
        {
            bases.Add(type.BaseType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
        }

        if (type.TypeKind == TypeKind.Enum && type.EnumUnderlyingType != null && type.EnumUnderlyingType.SpecialType != SpecialType.System_Int32)
        {
            bases.Add(type.EnumUnderlyingType.ToDisplayString());
        }

        if (type.TypeKind != TypeKind.Enum)
        {
            bases.AddRange(type.Interfaces.Where(i => i.DeclaredAccessibility == Accessibility.Public)
                .Select(i => i.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }

        if (bases.Count > 0) sb.Append(" : ").Append(string.Join(", ", bases));
        return sb.ToString();
    }

    private static bool IsShownMember(ISymbol member, INamedTypeSymbol type)
    {
        if (member.IsImplicitlyDeclared) return false;
        if (member.Name.Contains("<") || member.Name.Contains("__")) return false;
        var access = member.DeclaredAccessibility;
        var visible = access == Accessibility.Public ||
                      (!type.IsSealed && !type.IsValueType && (access == Accessibility.Protected || access == Accessibility.ProtectedOrInternal));
        if (!visible) return false;
        return member switch
        {
            IMethodSymbol m => m.MethodKind is MethodKind.Ordinary or MethodKind.Constructor or MethodKind.UserDefinedOperator or MethodKind.Conversion
                               && !(m.MethodKind == MethodKind.Constructor && m.Parameters.Length == 0 && type.IsValueType),
            IPropertySymbol => true,
            IFieldSymbol => true,
            IEventSymbol => true,
            _ => false
        };
    }

    private static void WriteType(StringBuilder md, StringBuilder flat, INamedTypeSymbol type, Dictionary<string, string> docs,
        SortedDictionary<string, string> usedDocs)
    {
        var full = FullName(type);
        md.AppendLine($"## {DisplayName(type)}");
        md.AppendLine();
        md.AppendLine("```csharp");
        md.AppendLine(TypeHeader(type));
        md.AppendLine("```");
        md.AppendLine();
        if (docs.TryGetValue(full, out var typeDoc))
        {
            md.AppendLine(typeDoc);
            md.AppendLine();
            usedDocs[full] = typeDoc;
        }

        flat.AppendLine($"{full} :: {TypeHeader(type).Replace(Environment.NewLine, " ").Replace("\n", " ")}");

        var members = type.GetMembers().Where(m => IsShownMember(m, type)).ToList();
        if (type.TypeKind == TypeKind.Enum)
        {
            var values = members.OfType<IFieldSymbol>().Where(f => f.HasConstantValue)
                .Select(f => $"{f.Name} = {f.ConstantValue}").ToList();
            if (values.Count > 0)
            {
                md.AppendLine("Values: " + string.Join(", ", values.Select(v => $"`{v}`")));
                md.AppendLine();
                foreach (var value in values) flat.AppendLine($"{full} :: {value}");
            }

            return;
        }

        if (members.Count == 0) return;

        md.AppendLine("| Member | Summary |");
        md.AppendLine("|---|---|");
        foreach (var member in members.OrderBy(MemberOrder).ThenBy(m => m.Name, StringComparer.Ordinal))
        {
            var signature = member.ToDisplayString(MemberFormat);
            var attrs = Attributes(member);
            if (attrs.Length > 0) signature = attrs + " " + signature;
            var key = DocKey(full, member);
            docs.TryGetValue(key, out var doc);
            if (doc != null) usedDocs[key] = doc;
            md.AppendLine($"| `{signature.Replace("|", "\\|")}` | {(doc ?? "").Replace("|", "\\|")} |");
            flat.AppendLine($"{full} :: {signature}");
        }

        md.AppendLine();
    }

    private static int MemberOrder(ISymbol m) => m switch
    {
        IFieldSymbol => 0,
        IPropertySymbol => 1,
        IEventSymbol => 2,
        IMethodSymbol { MethodKind: MethodKind.Constructor } => 3,
        _ => 4
    };

    internal static string DocKey(string typeFullName, ISymbol member)
    {
        var name = member is IMethodSymbol { MethodKind: MethodKind.Constructor } ? "ctor" : member.Name;
        return member is IMethodSymbol method ? $"{typeFullName}.{name}({method.Parameters.Length})" : $"{typeFullName}.{name}";
    }
}

/// <summary>
///   Reads `///` doc comments from the game's source, by syntax only (no semantic model, so no Unity needed),
///   and keys them the way <see cref="Program.DocKey" /> keys members: Namespace.Type[.Nested].Member(paramCount).
///   Only the first paragraph of each summary is kept, with internal references (file paths, spec and task ids)
///   removed, and cut at a sentence end before the length limit.
/// </summary>
internal static class DocCommentReader
{
    public static Dictionary<string, string> Read(string sourceDir, int maxChars)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*.cs", SearchOption.AllDirectories))
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file),
                new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Parse));
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                switch (node)
                {
                    case BaseTypeDeclarationSyntax type:
                        Add(result, TypeName(type), type, maxChars);
                        break;
                    case DelegateDeclarationSyntax del:
                        Add(result, Prefix(del) + del.Identifier.Text, del, maxChars);
                        break;
                    case MethodDeclarationSyntax m when m.Parent is BaseTypeDeclarationSyntax t:
                        Add(result, $"{TypeName(t)}.{m.Identifier.Text}({m.ParameterList.Parameters.Count})", m, maxChars);
                        break;
                    case ConstructorDeclarationSyntax c when c.Parent is BaseTypeDeclarationSyntax t:
                        Add(result, $"{TypeName(t)}.ctor({c.ParameterList.Parameters.Count})", c, maxChars);
                        break;
                    case PropertyDeclarationSyntax p when p.Parent is BaseTypeDeclarationSyntax t:
                        Add(result, $"{TypeName(t)}.{p.Identifier.Text}", p, maxChars);
                        break;
                    case FieldDeclarationSyntax f when f.Parent is BaseTypeDeclarationSyntax t:
                        foreach (var v in f.Declaration.Variables) Add(result, $"{TypeName(t)}.{v.Identifier.Text}", f, maxChars);
                        break;
                    case EnumMemberDeclarationSyntax e when e.Parent is BaseTypeDeclarationSyntax t:
                        Add(result, $"{TypeName(t)}.{e.Identifier.Text}", e, maxChars);
                        break;
                    case EventFieldDeclarationSyntax ev when ev.Parent is BaseTypeDeclarationSyntax t:
                        foreach (var v in ev.Declaration.Variables) Add(result, $"{TypeName(t)}.{v.Identifier.Text}", ev, maxChars);
                        break;
                }
            }
        }

        return result;
    }

    private static string Prefix(SyntaxNode node)
    {
        var parts = new List<string>();
        for (var p = node.Parent; p != null; p = p.Parent)
        {
            switch (p)
            {
                case BaseTypeDeclarationSyntax t: parts.Insert(0, t.Identifier.Text); break;
                case BaseNamespaceDeclarationSyntax n: parts.Insert(0, n.Name.ToString()); break;
            }
        }

        return parts.Count == 0 ? "" : string.Join(".", parts) + ".";
    }

    private static string TypeName(BaseTypeDeclarationSyntax type) => Prefix(type) + type.Identifier.Text;

    private static void Add(Dictionary<string, string> result, string key, SyntaxNode node, int maxChars)
    {
        if (result.ContainsKey(key)) return;
        var trivia = node.GetLeadingTrivia().Select(t => t.GetStructure()).OfType<DocumentationCommentTriviaSyntax>().FirstOrDefault();
        if (trivia == null) return;
        var summary = trivia.Content.OfType<XmlElementSyntax>().FirstOrDefault(e => e.StartTag.Name.ToString() == "summary");
        if (summary == null) return;
        var text = Clean(FirstParagraph(summary), maxChars);
        if (text.Length > 0) result[key] = text;
    }

    // The summary's own text up to its first <para>: the lead paragraph, which is what a mod author needs.
    private static string FirstParagraph(XmlElementSyntax summary)
    {
        var sb = new StringBuilder();
        foreach (var node in summary.Content)
        {
            if (node is XmlElementSyntax { StartTag.Name: var name } && name.ToString() == "para")
            {
                if (sb.ToString().Trim().Length > 0) break;
                sb.Append(Text(node));
                break;
            }

            sb.Append(Text(node));
        }

        return sb.ToString();
    }

    private static string Text(XmlNodeSyntax node)
    {
        switch (node)
        {
            case XmlTextSyntax text:
                return string.Concat(text.TextTokens.Select(t => t.Text));
            case XmlEmptyElementSyntax empty:
                var attr = empty.Attributes.FirstOrDefault();
                var value = attr switch
                {
                    XmlCrefAttributeSyntax c => c.Cref.ToString(),
                    XmlNameAttributeSyntax n => n.Identifier.ToString(),
                    XmlTextAttributeSyntax t => string.Concat(t.TextTokens.Select(x => x.Text)),
                    _ => ""
                };
                return value.Length > 0 ? $"`{value}`" : "";
            case XmlElementSyntax element:
                var inner = string.Concat(element.Content.Select(Text));
                return element.StartTag.Name.ToString() == "c" ? $"`{inner.Trim()}`" : inner;
            default:
                return node.ToString();
        }
    }

    private static readonly Regex[] Redactions =
    {
        // Parentheticals that cite source files, specs, tasks or features: "(Foo.cs:12-34)", "(055 R37)", "(feature 063)".
        new(@"\s*\((?=[^()]*(\.cs\b|specs/|Documentation/|docs/|\bfeature \d|\b\d{3}\b|\bw\d{2,4}\b|\bT\d{3}|#bug-reports|Discord))[^()]*\)", RegexOptions.Compiled),
        // Bracketed task tags: "[055 T044m]", "[073 T021]".
        new(@"\[\d{3}[^\]]*\]\s*", RegexOptions.Compiled),
        // "Feature 063:" / "Feature 073 (US6) —" lead-ins.
        new(@"^\s*Feature \d{3}[^:—-]*[:—-]\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // Bare file references left in running text.
        new(@"`?[\w./-]+\.cs(:\d+(-\d+)?)?`?", RegexOptions.Compiled),
        // Parentheticals that quote a person or a date: "(Ben, 2026-10-02: ...)", "(2026-09-28, ...)".
        new(@"\s*\((?=[^()]*(\d{4}-\d{2}-\d{2}|\bBen\b|\bLothsahn\b))[^()]*\)", RegexOptions.Compiled),
        // A dash clause that quotes a person: "... past the player — Ben's ""rubber band forward"" at 250 speed."
        new(@"\s*[—–]\s*[^.—–]*\b(Ben|Lothsahn)\b[^.—–]*", RegexOptions.Compiled),
        // Internal work-item ids: "w334: ...", "as before w408", "the pre-w334 bolt".
        new(@"\b(as )?before w\d{2,4}\b,?", RegexOptions.Compiled),
        new(@"\bpre-w\d{2,4}\s*", RegexOptions.Compiled),
        new(@"\bw\d{2,4}\b\s*[:—–-]?\s*", RegexOptions.Compiled),
        // Developer notes.
        new(@"(//\s*)?\bTODO\b.*$", RegexOptions.Compiled),
        new(@"\bHACK\b:?", RegexOptions.Compiled),
    };

    private static string Clean(string text, int maxChars)
    {
        text = Regex.Replace(text, @"\s*///\s*", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        foreach (var r in Redactions) text = r.Replace(text, "");
        text = Regex.Replace(text, @"\s+([,.;:])", "$1");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length > maxChars)
        {
            var cut = text.LastIndexOf(". ", maxChars, StringComparison.Ordinal);
            text = cut > maxChars / 3 ? text.Substring(0, cut + 1) : text.Substring(0, maxChars).TrimEnd() + "…";
        }

        return text;
    }
}
