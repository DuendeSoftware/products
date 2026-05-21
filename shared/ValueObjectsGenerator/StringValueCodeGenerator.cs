// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ValueObjectsGenerator;

/// <summary>
/// Generates .g.cs content for [StringValue] types.
/// The output is identical to what the Roslyn StringValues source generator produced.
/// </summary>
internal static class StringValueCodeGenerator
{
    internal static string Generate(ValueObjectInfo info)
    {
        var structName = info.TypeName;
        var namespaceName = info.Namespace;
        var internalCtor = info.HasInternalConstructor;

        var validationErrorChecks = BuildValidationErrorChecks(
            info.HasMaxLength, info.HasAllowedCharacters, info.HasRegex,
            info.HasAllowedCharSet, info.HasTryValidate);

        var normalizeCall = info.HasNormalize
            ? """

                s = Normalize(s);
                if (string.IsNullOrWhiteSpace(s))
                {
                    errors = ["Value is empty after normalization."];
                    return false;
                }
            """
            : string.Empty;

        var parse = internalCtor ? string.Empty :
            info.HasParse ? string.Empty :
            info.HasTryParse
            ? AddOverridable(
                $$"""
                public static {{structName}} Create(string s)
                {
                    if (!TryCreate(s, out var result))
                    {
                        throw new FormatException($"The value '{s}' is not a valid {{structName}}.");
                    }
                    return result;
                }
                """,
                4)
            : AddOverridable(
                $$"""
                public static {{structName}} Create(string s)
                {
                    if (!TryCreate(s, out var result, out var errors))
                    {
                        throw new FormatException($"The value '{s}' is not a valid {{structName}}. {string.Join(" ", errors)}");
                    }
                    return result;
                }
                """,
                4);

        var tryParse = internalCtor ? string.Empty :
            info.HasTryParse ? string.Empty : AddOverridable(
            $$"""
            public static bool TryCreate(string? s, [NotNullWhen(true)] out {{structName}}? result)
                => TryCreate(s, out result, out _);

            public static bool TryCreate(string? s, [NotNullWhen(true)] out {{structName}}? result, [NotNullWhen(false)] out IReadOnlyList<string>? errors)
            {
                result = null;
                errors = null;
                if (string.IsNullOrWhiteSpace(s))
                {
                    errors = ["A value is required."];
                    return false;
                }
            {{normalizeCall}}{{validationErrorChecks}}
                result = new {{structName}}(s);
                return true;
            }
            """,
            4);

        var toString = (info.GenerateToString && !info.HasToString)
            ? $$"""
                    public override string ToString() => Value;
                    """ : string.Empty;

        var collectionsUsing = (!internalCtor && !info.HasTryParse) ? "using System.Collections.Generic;\n" : string.Empty;

        // When the type's namespace differs from the root namespace (where IStringValue etc. live),
        // we need an explicit using directive so the generated code can find the infrastructure types.
        var needsRootNamespace = !info.HasInternalValue &&
                                 !string.Equals(info.Namespace, info.RootNamespace, StringComparison.Ordinal);
        var rootNamespaceUsing = needsRootNamespace
            ? $"using {info.RootNamespace};\n"
            : string.Empty;

        var equalityMethods = info.HasComparer
            ? $$"""

                    public virtual bool Equals({{structName}}? other) =>
                        other is not null && Comparer.Equals(Value, other.Value);

                    public override int GetHashCode() =>
                        Value is null ? 0 : Comparer.GetHashCode(Value);

                """
            : string.Empty;

        // When HasInternalConstructor: use internal constructor, skip implicit operator and ParseOrDefault
        var loadFromStorage = info.HasLoadFromStorage ? string.Empty :
            $"internal static {structName} Load(string value) => new {structName}(value);";

        var ctorVisibility = internalCtor ? "internal" : "private";
        // When HasInternalConstructor, the user provides the constructor in the hand-written partial — skip emitting it
        var ctorDeclaration = internalCtor ? string.Empty :
            $"// Constructor for controlled creation\n    {ctorVisibility} {structName}(string value) => Value = value;";

        var valueProperty = info.HasValue ? string.Empty : "public string Value { get; }";

        var implicitOperatorAndParseOrDefault = internalCtor ? string.Empty :
            $$"""

                public static implicit operator {{structName}}(string value) => Create(value);

                {{toString}}

                public static {{structName}}? CreateOrDefault(string? input)
                {
                    if (string.IsNullOrEmpty(input))
                    {
                        return null;
                    }

                    return Create(input);
                }
            """;

        var toStringWhenInternal = internalCtor
            ? $$"""

                {{toString}}
            """
            : string.Empty;

        var source = $$"""
                       // <auto-generated by="ValueObjectsGenerator"/>
                       // Copyright (c) Duende Software. All rights reserved.
                       // See LICENSE in the project root for license information.
                       #nullable enable

                       {{collectionsUsing}}{{rootNamespaceUsing}}{{(!internalCtor && !info.HasTryParse ? "using System.Diagnostics.CodeAnalysis;\n" : string.Empty)}}
                       namespace {{namespaceName}};

                       {{(internalCtor || info.HasInternalValue ? string.Empty : $"[System.ComponentModel.TypeConverter(typeof(ValueOfTypeConverter<{structName}, string>))]\n")}}partial record {{structName}}{{(info.HasInternalValue ? string.Empty : (internalCtor ? " : IValueOf<string>" : $" : IStringValue<{structName}>"))}}
                       {
                           {{ctorDeclaration}}

                           {{valueProperty}}

                           {{parse}}

                           {{tryParse}}
                       {{implicitOperatorAndParseOrDefault}}{{toStringWhenInternal}}

                           {{loadFromStorage}}
                       {{equalityMethods}}
                       }

                       """;

        return CollapseBlankLines(source.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    /// <summary>
    /// Collapses runs of multiple blank lines into a single blank line and trims trailing whitespace from lines.
    /// </summary>
    internal static string CollapseBlankLines(string text)
    {
        var lines = text.Split('\n');
        var result = new List<string>(lines.Length);
        var previousWasBlank = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd();
            var isBlank = trimmed.Length == 0;

            if (isBlank && previousWasBlank)
            {
                continue;
            }

            result.Add(trimmed);
            previousWasBlank = isBlank;
        }

        return string.Join("\n", result);
    }

    private static string BuildValidationErrorChecks(
        bool hasMaxLength,
        bool hasAllowedCharacters,
        bool hasRegex,
        bool hasAllowedCharSet,
        bool hasTryValidate)
    {
        var hasAnyRule = hasMaxLength || hasAllowedCharacters || hasRegex || hasAllowedCharSet || hasTryValidate;
        if (!hasAnyRule)
        {
            return string.Empty;
        }

        var checks = new List<string>();

        checks.Add("""

                var validationErrors = new List<string>();
            """);

        if (hasMaxLength)
        {
            checks.Add("""

                if (s.Length > MaxLength)
                {
                    validationErrors.Add($"Must not exceed {MaxLength} characters.");
                }
            """);
        }

        if (hasAllowedCharacters)
        {
            checks.Add("""

                if (s.Any(c => !AllowedCharacters.Contains(c)))
                {
                    validationErrors.Add("Must only contain allowed characters.");
                }
            """);
        }

        if (hasRegex)
        {
            checks.Add("""

                if (!Regex().IsMatch(s))
                {
                    validationErrors.Add("Must match the required pattern.");
                }
            """);
        }

        if (hasAllowedCharSet)
        {
            checks.Add("""

                if (!AllowedCharSet.IsMatch(s))
                {
                    validationErrors.Add("Must only contain allowed character types.");
                }
            """);
        }

        if (hasTryValidate)
        {
            checks.Add("""

                if (!TryValidate(s, out var tryValidateErrors))
                {
                    if (tryValidateErrors is { Count: > 0 })
                    {
                        validationErrors.AddRange(tryValidateErrors);
                    }
                    else
                    {
                        validationErrors.Add($"The value '{s}' is not valid.");
                    }
                }
            """);
        }

        checks.Add("""

                if (validationErrors.Count > 0)
                {
                    errors = validationErrors;
                    return false;
                }
            """);

        return string.Join(string.Empty, checks);
    }

    /// <summary>
    /// Adds indentation to each line (except the first) of the content block.
    /// Mirrors the AddOverridable helper in the source generator.
    /// </summary>
    private static string AddOverridable(string content, int leadingSpaces)
    {
        var indent = new string(' ', leadingSpaces);
        var lines = content.Split('\n');
        var indentedLines = lines.Select((line, index) =>
        {
            if (index == 0)
            {
                return line;
            }

            return string.IsNullOrEmpty(line.TrimEnd()) ? line : indent + line;
        });
        return string.Join("\n", indentedLines);
    }
}
