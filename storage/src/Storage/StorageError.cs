// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage;

/// <summary>
/// Represents an error that occurred during a storage operation.
/// </summary>
/// <param name="Code">A machine-readable error code.</param>
/// <param name="Message">A human-readable error description.</param>
/// <param name="PropertyNames">Optional names of properties that are affected by this error.</param>
public sealed record StorageError(
    string Code,
    string Message,
    IReadOnlyList<string>? PropertyNames = null)
{
    /// <summary>
    /// Creates an error indicating that an item already exists.
    /// </summary>
    public static StorageError AlreadyExists(string itemType, string identifier, params string[] propertyNames) =>
        new("already_exists", $"A {itemType} with identifier '{identifier}' already exists.", propertyNames);

    /// <summary>
    /// Creates an error indicating that an item was not found.
    /// </summary>
    public static StorageError NotFound(string itemType, string identifier) =>
        new("not_found", $"The {itemType} with identifier '{identifier}' was not found.");

    /// <summary>
    /// Creates an error indicating that a unique constraint was violated.
    /// </summary>
    public static StorageError DuplicateValue(string propertyName, string value) =>
        new("duplicate_value", $"The value '{value}' for {propertyName} is already in use.", [propertyName]);

    /// <summary>
    /// Creates an error indicating that a version conflict occurred (optimistic concurrency).
    /// </summary>
    public static StorageError VersionConflict() =>
        new("version_conflict", "The item has been modified by another operation. Please refresh and try again.");

    /// <summary>
    /// Creates an error indicating that validation failed.
    /// </summary>
    public static StorageError ValidationFailed(string message, params string[] propertyNames) =>
        new("validation_failed", message, propertyNames);

    /// <summary>
    /// Creates an error indicating that a required property is missing.
    /// </summary>
    public static StorageError Required(string propertyName) =>
        new("required", $"The property '{propertyName}' is required.", [propertyName]);

    /// <summary>
    /// Creates an error indicating that a property value is invalid.
    /// </summary>
    public static StorageError InvalidValue(string propertyName, string reason) =>
        new("invalid_value", $"The value for '{propertyName}' is invalid: {reason}", [propertyName]);

    /// <inheritdoc/>
    public override string ToString() =>
        PropertyNames is { Count: > 0 }
            ? $"{Code}: {Message} [{string.Join(", ", PropertyNames)}]"
            : $"{Code}: {Message}";
}
