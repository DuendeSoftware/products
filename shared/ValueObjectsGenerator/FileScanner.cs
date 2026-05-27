// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Duende.ValueObjectsGenerator;

/// <summary>
/// Scans a directory for C# files containing value object types annotated with
/// [StringValue] or [ValueOf&lt;T&gt;] and builds ValueObjectInfo descriptors.
/// </summary>
internal static class FileScanner
{
    // Well-known C# keyword → fully-qualified global type name mappings
    private static readonly Dictionary<string, string> WellKnownTypes = new(StringComparer.Ordinal)
    {
        ["bool"] = "global::System.Boolean",
        ["byte"] = "global::System.Byte",
        ["char"] = "global::System.Char",
        ["decimal"] = "global::System.Decimal",
        ["double"] = "global::System.Double",
        ["float"] = "global::System.Single",
        ["int"] = "global::System.Int32",
        ["long"] = "global::System.Int64",
        ["sbyte"] = "global::System.SByte",
        ["short"] = "global::System.Int16",
        ["uint"] = "global::System.UInt32",
        ["ulong"] = "global::System.UInt64",
        ["ushort"] = "global::System.UInt16",
        // Common non-keyword structs that are always System.*
        ["Guid"] = "global::System.Guid",
        ["DateTime"] = "global::System.DateTime",
        ["DateOnly"] = "global::System.DateOnly",
        ["TimeOnly"] = "global::System.TimeOnly",
        ["TimeSpan"] = "global::System.TimeSpan",
        ["DateTimeOffset"] = "global::System.DateTimeOffset",
        ["Uri"] = "global::System.Uri",
    };

    /// <summary>
    /// Scans <paramref name="rootPath"/> recursively and returns all discovered value objects.
    /// </summary>
    internal static IReadOnlyList<ValueObjectInfo> Scan(string rootPath, string rootNamespace)
    {
        var csFiles = Directory
            .EnumerateFiles(rootPath, "*.cs", SearchOption.AllDirectories)
            .Where(IsEligibleFile)
            .ToList();

        // Pass 1: collect candidate records and build a lookup of all VO type names → namespace
        var rawCandidates = new List<RawCandidate>();
        foreach (var file in csFiles)
        {
            var text = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(text);
            var root = tree.GetCompilationUnitRoot();
            CollectCandidates(file, root, rawCandidates);
        }

        // Build a lookup of all value-object type names → namespace for nested detection.
        // Use a dictionary keyed by simple type name. If multiple types share the same short name
        // (different namespaces), the first one wins — this is safe because cross-namespace nesting
        // within a single project would be ambiguous at the C# level too.
        var valueObjectTypeMap = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var c in rawCandidates)
        {
            _ = valueObjectTypeMap.TryAdd(c.TypeName, c.Namespace);
        }

        // Pass 2: resolve full info (including nested detection and FQN resolution)
        var results = new List<ValueObjectInfo>(rawCandidates.Count);
        foreach (var raw in rawCandidates)
        {
            string? genericTypeArgument = null;
            var isNested = false;

            if (raw.Kind == ValueObjectKind.ValueOf && raw.RawGenericArg is { } rawArg)
            {
                genericTypeArgument = ResolveGenericTypeArgument(rawArg, raw.Namespace, raw.UsingNamespaces, valueObjectTypeMap);

                // Normalize to the simple type name (rightmost identifier) for nested detection.
                // Handles "global::Some.Namespace.MyType", "Some.Namespace.MyType", and "MyType".
                var simpleArg = rawArg.Trim();
                if (simpleArg.StartsWith("global::", StringComparison.Ordinal))
                {
                    simpleArg = simpleArg["global::".Length..];
                }

                var lastDot = simpleArg.LastIndexOf('.');
                if (lastDot >= 0)
                {
                    simpleArg = simpleArg[(lastDot + 1)..];
                }

                isNested = valueObjectTypeMap.ContainsKey(simpleArg);
            }

            results.Add(new ValueObjectInfo(
                SourceFilePath: raw.SourceFilePath,
                TypeName: raw.TypeName,
                Namespace: raw.Namespace,
                RootNamespace: rootNamespace,
                Kind: raw.Kind,
                GenericTypeArgument: genericTypeArgument,
                IsNestedValueObject: isNested,
                HasMaxLength: raw.HasMaxLength,
                HasAllowedCharacters: raw.HasAllowedCharacters,
                HasRegex: raw.HasRegex,
                HasAllowedCharSet: raw.HasAllowedCharSet,
                HasTryValidate: raw.HasTryValidate,
                HasErrorMessage: raw.HasErrorMessage,
                HasParse: raw.HasParse,
                HasTryParse: raw.HasTryParse,
                HasNormalize: raw.HasNormalize,
                GenerateToString: raw.GenerateToString,
                HasComparer: raw.HasComparer,
                HasInternalConstructor: raw.HasInternalConstructor,
                HasValue: raw.HasValue,
                HasInternalValue: raw.HasInternalValue,
                HasLoadFromStorage: raw.HasLoadFromStorage,
                HasToString: raw.HasToString));
        }

        return results;
    }

    private static bool IsEligibleFile(string path)
    {
        // Skip generated files and build output
        if (path.EndsWith(".g.cs", StringComparison.Ordinal))
        {
            return false;
        }

        var normalized = path.Replace('\\', '/');

        if (normalized.Contains("/obj/", StringComparison.Ordinal) || normalized.Contains("/bin/", StringComparison.Ordinal) || normalized.Contains("/Generated/", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static void CollectCandidates(string filePath, CompilationUnitSyntax root, List<RawCandidate> results)
    {
        // Collect using directives for namespace resolution
        var usingNamespaces = root.Usings
            .Select(u => u.Name?.ToString() ?? string.Empty)
            .Where(u => !string.IsNullOrEmpty(u))
            .ToList();

        foreach (var record in root.DescendantNodes().OfType<RecordDeclarationSyntax>())
        {
            if (!record.Modifiers.Any(SyntaxKind.PartialKeyword))
            {
                continue;
            }

            if (!IsRecordClass(record))
            {
                continue;
            }

            var (kind, rawArg, generateToString) = DetectAttribute(record);
            if (kind is null)
            {
                continue;
            }

            var ns = GetNamespace(record);
            var typeName = record.Identifier.Text;
            var members = record.Members;

            var candidate = new RawCandidate(
                SourceFilePath: filePath,
                TypeName: typeName,
                Namespace: ns,
                Kind: kind.Value,
                RawGenericArg: rawArg,
                UsingNamespaces: usingNamespaces,
                HasMaxLength: HasMember(members, "MaxLength"),
                HasAllowedCharacters: HasMember(members, "AllowedCharacters"),
                HasRegex: HasMethod(members, "Regex"),
                HasAllowedCharSet: HasMember(members, "AllowedCharSet"),
                HasTryValidate: HasMethod(members, "TryValidate"),
                HasErrorMessage: HasMember(members, "ErrorMessage"),
                HasParse: HasMethod(members, "Create"),
                HasTryParse: HasMethod(members, "TryCreate"),
                HasNormalize: HasMethod(members, "Normalize"),
                HasComparer: HasMember(members, "Comparer"),
                HasInternalConstructor: HasInternalConstructor(members, typeName),
                HasValue: HasMember(members, "Value"),
                HasInternalValue: HasInternalProperty(members, "Value"),
                HasLoadFromStorage: HasMethod(members, "Load"),
                HasToString: HasMethod(members, "ToString"),
                GenerateToString: generateToString
            );

            results.Add(candidate);
        }
    }

    private static bool IsRecordClass(RecordDeclarationSyntax record) =>
        !record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword);

    private static (ValueObjectKind? kind, string? rawArg, bool generateToString) DetectAttribute(RecordDeclarationSyntax record)
    {
        foreach (var attrList in record.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();

                if (name == "StringValue" || name == "StringValueAttribute" ||
                    name == "Duende.StringValue" || name == "Duende.StringValueAttribute")
                {
                    return (ValueObjectKind.StringValue, null, ExtractGenerateToString(attr));
                }

                if (name.StartsWith("ValueOf<", StringComparison.Ordinal) ||
                    name.StartsWith("ValueOfAttribute<", StringComparison.Ordinal) ||
                    name.StartsWith("Duende.ValueOf<", StringComparison.Ordinal) ||
                    name.StartsWith("Duende.ValueOfAttribute<", StringComparison.Ordinal))
                {
                    string? rawArg = null;
                    if (attr.Name is GenericNameSyntax generic && generic.TypeArgumentList.Arguments.Count > 0)
                    {
                        rawArg = generic.TypeArgumentList.Arguments[0].ToString().Trim();
                    }
                    return (ValueObjectKind.ValueOf, rawArg, ExtractGenerateToString(attr));
                }
            }
        }

        return (null, null, true);
    }

    private static bool ExtractGenerateToString(AttributeSyntax attr)
    {
        if (attr.ArgumentList is null)
        {
            return true;
        }

        foreach (var arg in attr.ArgumentList.Arguments)
        {
            if (arg.NameEquals?.Name.ToString() == "GenerateToString" &&
                arg.Expression is LiteralExpressionSyntax lit)
            {
                return !lit.IsKind(SyntaxKind.FalseLiteralExpression);
            }
        }

        return true;
    }

    private static bool HasInternalConstructor(SyntaxList<MemberDeclarationSyntax> members, string typeName) =>
        members.OfType<ConstructorDeclarationSyntax>().Any(c =>
            c.Identifier.Text == typeName &&
            c.Modifiers.Any(SyntaxKind.InternalKeyword));

    private static bool HasMember(SyntaxList<MemberDeclarationSyntax> members, string name) =>
        members.Any(m => GetMemberName(m) == name);

    private static bool HasInternalProperty(SyntaxList<MemberDeclarationSyntax> members, string name) =>
        members.OfType<PropertyDeclarationSyntax>()
            .Any(p => p.Identifier.Text == name &&
                      p.Modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword)));

    private static bool HasMethod(SyntaxList<MemberDeclarationSyntax> members, string name) =>
        members.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.Text == name);

    private static string? GetMemberName(MemberDeclarationSyntax member) => member switch
    {
        FieldDeclarationSyntax f => f.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
        PropertyDeclarationSyntax p => p.Identifier.Text,
        MethodDeclarationSyntax m => m.Identifier.Text,
        _ => null
    };

    private static string GetNamespace(BaseTypeDeclarationSyntax syntax)
    {
        var ns = string.Empty;
        var parent = syntax.Parent;

        while (parent is not null &&
               parent is not NamespaceDeclarationSyntax &&
               parent is not FileScopedNamespaceDeclarationSyntax)
        {
            parent = parent.Parent;
        }

        if (parent is BaseNamespaceDeclarationSyntax nsDecl)
        {
            ns = nsDecl.Name.ToString();

            while (true)
            {
                if (nsDecl.Parent is not NamespaceDeclarationSyntax outerNs)
                {
                    break;
                }

                ns = $"{outerNs.Name}.{ns}";
                nsDecl = outerNs;
            }
        }

        return ns;
    }

    /// <summary>
    /// Resolves a raw generic type argument string to a fully-qualified global:: name.
    /// </summary>
    private static string ResolveGenericTypeArgument(
        string rawArg,
        string currentNamespace,
        IReadOnlyList<string> usingNamespaces,
        Dictionary<string, string> valueObjectTypeMap)
    {
        var trimmed = rawArg.Trim();

        // Already fully qualified with global:: prefix
        if (trimmed.StartsWith("global::", StringComparison.Ordinal))
        {
            return trimmed;
        }

        // Well-known primitive / BCL struct (e.g. int, Guid, DateTime)
        if (WellKnownTypes.TryGetValue(trimmed, out var fqn))
        {
            return fqn;
        }

        // Already namespace-qualified (contains a dot, e.g. "Some.Namespace.MyType")
        // — treat as fully qualified, just add global:: prefix
        if (trimmed.Contains('.', StringComparison.Ordinal))
        {
            return $"global::{trimmed}";
        }

        // Check if it's a known value object type in the same project
        if (valueObjectTypeMap.TryGetValue(trimmed, out var voNamespace))
        {
            return $"global::{voNamespace}.{trimmed}";
        }

        // Try to resolve from using directives — the type could be imported via a using.
        // For simple (unqualified) names, check if using namespace + type name forms a known type.
        foreach (var usingNs in usingNamespaces)
        {
            var candidate = $"{usingNs}.{trimmed}";

            // Check well-known types with the fully-qualified candidate
            if (WellKnownTypes.TryGetValue(candidate, out var resolved))
            {
                return resolved;
            }

            // Check if using namespace + simple name matches a known value object
            if (valueObjectTypeMap.TryGetValue(trimmed, out var voNs) && voNs == usingNs)
            {
                return $"global::{voNs}.{trimmed}";
            }
        }

        // Fall back to the current namespace
        return $"global::{currentNamespace}.{trimmed}";
    }

    // Internal record for first-pass collection
    private sealed record RawCandidate(
        string SourceFilePath,
        string TypeName,
        string Namespace,
        ValueObjectKind Kind,
        string? RawGenericArg,
        IReadOnlyList<string> UsingNamespaces,
        bool HasMaxLength,
        bool HasAllowedCharacters,
        bool HasRegex,
        bool HasAllowedCharSet,
        bool HasTryValidate,
        bool HasErrorMessage,
        bool HasParse,
        bool HasTryParse,
        bool HasNormalize,
        bool HasComparer,
        bool HasInternalConstructor,
        bool HasValue,
        bool HasInternalValue,
        bool HasLoadFromStorage,
        bool HasToString,
        bool GenerateToString
    );
}
