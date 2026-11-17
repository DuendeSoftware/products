// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Represents an error that occurred during a storage operation.
/// </summary>
/// <param name="Code">A machine-readable error code.</param>
/// <param name="Message">A human-readable error description.</param>
/// <param name="PropertyNames">Optional names of properties that are affected by this error.</param>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public sealed record ErrorResult(
    string Code,
    string Message,
    IReadOnlyList<string>? PropertyNames = null)
{
    /// <summary>
    /// Creates an error indicating that an item already exists.
    /// </summary>
    public static ErrorResult AlreadyExists(string itemType, string identifier, params string[] propertyNames) =>
        new("already_exists", $"A {itemType} with identifier '{identifier}' already exists.", propertyNames);

    /// <summary>
    /// Creates an error indicating that an item was not found.
    /// </summary>
    public static ErrorResult NotFound(string itemType, string identifier) =>
        new("not_found", $"The {itemType} with identifier '{identifier}' was not found.");

    /// <summary>
    /// Creates an error indicating that a unique constraint was violated.
    /// </summary>
    public static ErrorResult DuplicateValue(string propertyName, string value) =>
        new("duplicate_value", $"The value '{value}' for {propertyName} is already in use.", [propertyName]);

    /// <summary>
    /// Creates an error indicating that a version conflict occurred (optimistic concurrency).
    /// </summary>
    public static ErrorResult VersionConflict() =>
        new("version_conflict", "The item has been modified by another operation. Please refresh and try again.");

    /// <summary>
    /// Creates an error indicating that validation failed.
    /// </summary>
    public static ErrorResult ValidationFailed(string message, params string[] propertyNames) =>
        new("validation_failed", message, propertyNames);

    /// <summary>
    /// Creates an error indicating that a required property is missing.
    /// </summary>
    public static ErrorResult Required(string propertyName) =>
        new("required", $"The property '{propertyName}' is required.", [propertyName]);

    /// <summary>
    /// Creates an error indicating that a property value is invalid.
    /// </summary>
    public static ErrorResult InvalidValue(string propertyName, string reason) =>
        new("invalid_value", $"The value for '{propertyName}' is invalid: {reason}", [propertyName]);

    /// <inheritdoc/>
    public override string ToString() =>
        PropertyNames is { Count: > 0 }
            ? $"{Code}: {Message} [{string.Join(", ", PropertyNames)}]"
            : $"{Code}: {Message}";
}
