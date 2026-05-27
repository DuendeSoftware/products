// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.ValueObjectsGenerator;

/// <summary>
/// Describes the kind of value object: string-based or generic.
/// </summary>
internal enum ValueObjectKind
{
    StringValue,
    ValueOf
}

/// <summary>
/// All information needed to generate code for one value object type.
/// </summary>
internal sealed record ValueObjectInfo(
    string SourceFilePath,
    string TypeName,
    string Namespace,
    string RootNamespace,
    ValueObjectKind Kind,
    // ValueOf only — fully-qualified generic type argument, e.g. "global::System.Guid"
    string? GenericTypeArgument,
    // Whether the generic type argument is itself a value object (nested)
    bool IsNestedValueObject,
    bool HasMaxLength,
    bool HasAllowedCharacters,
    bool HasRegex,
    bool HasAllowedCharSet,
    bool HasTryValidate,
    bool HasErrorMessage,
    bool HasParse,
    bool HasTryParse,
    bool HasNormalize,
    bool GenerateToString,
    bool HasComparer,
    bool HasInternalConstructor,
    bool HasValue,
    bool HasInternalValue,
    bool HasLoadFromStorage,
    bool HasToString
);
