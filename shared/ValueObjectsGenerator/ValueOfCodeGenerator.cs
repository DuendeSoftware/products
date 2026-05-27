// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ValueObjectsGenerator;

/// <summary>
/// Generates .g.cs content for [ValueOf&lt;T&gt;] types.
/// The output is identical to what the Roslyn ValueOf source generator produced.
/// </summary>
internal static class ValueOfCodeGenerator
{
    internal static string Generate(ValueObjectInfo info)
    {
        var structName = info.TypeName;
        var namespaceName = info.Namespace;
        var genericTypeArgument = info.GenericTypeArgument ?? "global::System.Object";
        var isNested = info.IsNestedValueObject;
        var internalCtor = info.HasInternalConstructor;

        // Validation check inside TryParse errors overload when TryValidate exists
        var validationErrorCheck = info.HasTryValidate
            ? $$"""

                    if (!TryValidate(value, out var tryValidateErrors))
                    {
                        errors = tryValidateErrors is { Count: > 0 } ? tryValidateErrors : [$"The value is not a valid '{nameof({{structName}})}'."];
                        return false;
                    }
            """
            : string.Empty;

        // Implicit operator body
        var implicitOperatorCheck = info.HasTryValidate
            ? AddOverridable($$"""
            if (!TryValidate(value, out var errors))
            {
                var errorMessage = $"The value '{value}' is not a valid '{nameof({{structName}})}'. {string.Join(" ", errors ?? [])}";
                throw new FormatException(errorMessage);
            }
            return new {{structName}}(value);
            """, 8)
            : $"return new {structName}(value);";

        // Create method
        var parse = internalCtor ? string.Empty :
                    info.HasParse ? string.Empty :
                    info.HasTryParse
                    ? AddOverridable($$"""
                    public static {{structName}} Create(string s)
                    {
                        if (!TryCreate(s, out var result))
                        {
                            throw new FormatException($"The value '{s}' is not a valid '{nameof({{structName}})}'.");
                        }
                        return result;
                    }
                    """, 4)
                    : AddOverridable($$"""
                    public static {{structName}} Create(string s)
                    {
                        if (!TryCreate(s, out var result, out var errors))
                        {
                            throw new FormatException($"The value '{s}' is not a valid '{nameof({{structName}})}'. {string.Join(" ", errors)}");
                        }
                        return result;
                    }
                    """, 4);

        // TryParse inner call varies on whether T is itself a value object
        var tryParseInnerCall = isNested
            ? $"{genericTypeArgument}.TryCreate(s, out var value)"
            : $"{genericTypeArgument}.TryParse(s, CultureInfo.InvariantCulture, out var value)";

        // TryCreate method
        var tryParse = internalCtor ? string.Empty :
                     info.HasTryParse ? string.Empty :
                     AddOverridable($$"""
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

                         if ({{tryParseInnerCall}})
                         {{{validationErrorCheck}}
                             var instance = new {{structName}}(value);
                             result = instance;
                             return true;
                         }

                         errors = ["The value could not be parsed."];
                         return false;
                     }
                     """, 4);

        // ToString override
        var toString = (info.GenerateToString && !info.HasToString)
            ? (isNested
                ? "public override string ToString() => Value.ToString();"
                : "public override string ToString() => Value.ToString(null, CultureInfo.InvariantCulture);")
            : string.Empty;

        var collectionsUsing = (!internalCtor && !info.HasTryParse) ? "using System.Collections.Generic;\n" : string.Empty;

        var rootNamespaceUsing = !string.Equals(info.Namespace, info.RootNamespace, StringComparison.Ordinal)
            ? $"using {info.RootNamespace};\n"
            : string.Empty;

        var loadFromStorage = info.HasLoadFromStorage ? string.Empty :
            $"internal static {structName} Load({genericTypeArgument} value) => new {structName}(value);";

        var ctorVisibility = internalCtor ? "internal" : "private";

        var valueProperty = info.HasValue ? string.Empty : $"public {genericTypeArgument} Value {{ get; }}";

        var implicitOperatorAndParseOrDefault = internalCtor ? string.Empty :
            $$"""

                public static implicit operator {{structName}}({{genericTypeArgument}} value)
                {
                    {{implicitOperatorCheck}}
                }

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

                       {{collectionsUsing}}{{rootNamespaceUsing}}{{(internalCtor ? string.Empty : "using System.Globalization;\n")}}{{(!internalCtor && !info.HasTryParse ? "using System.Diagnostics.CodeAnalysis;\n" : string.Empty)}}
                       namespace {{namespaceName}};

                       {{(internalCtor ? string.Empty : $"[System.ComponentModel.TypeConverter(typeof(ValueOfTypeConverter<{structName}, {genericTypeArgument}>))]\n")}}partial record {{structName}} : {{(internalCtor ? $"IValueOf<{genericTypeArgument}>" : $"IValueOf<{structName}, {genericTypeArgument}>")}}
                       {
                           // Constructor for controlled creation
                           {{ctorVisibility}} {{structName}}({{genericTypeArgument}} value) => Value = value;

                           {{valueProperty}}

                           {{parse}}

                           {{tryParse}}
                       {{implicitOperatorAndParseOrDefault}}{{toStringWhenInternal}}

                           {{loadFromStorage}}
                       }

                       """;

        return StringValueCodeGenerator.CollapseBlankLines(source.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    /// <summary>
    /// Adds indentation to each line (except the first) of the content block.
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
