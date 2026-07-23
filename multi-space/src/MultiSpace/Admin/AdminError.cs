// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.MultiSpace;

/// <summary>
/// Represents an error that occurred during an admin operation.
/// </summary>
/// <param name="Code">A machine-readable error code.</param>
/// <param name="Message">A human-readable error description.</param>
/// <param name="PropertyNames">Optional names of properties that are affected by this error.</param>
public sealed record AdminError(
    string Code,
    string Message,
    IReadOnlyList<string>? PropertyNames = null)
{
    /// <summary>Creates an error indicating that a resource already exists.</summary>
    public static AdminError AlreadyExists(string resourceType, string identifier, params string[] propertyNames) =>
        new("already_exists", $"A {resourceType} with identifier '{identifier}' already exists.", propertyNames);

    /// <summary>Creates an error indicating that a resource was not found.</summary>
    public static AdminError NotFound(string resourceType, string identifier) =>
        new("not_found", $"The {resourceType} with identifier '{identifier}' was not found.");

    /// <summary>Creates an error indicating that a version conflict occurred (optimistic concurrency).</summary>
    public static AdminError VersionConflict() =>
        new("version_conflict", "The resource has been modified. Refresh and retry.");

    /// <summary>Creates an error indicating that validation failed.</summary>
    public static AdminError ValidationFailed(string message, params string[] propertyNames) =>
        new("validation_failed", message, propertyNames);

    /// <inheritdoc/>
    public override string ToString() => $"{Code}: {Message} {string.Join(',', PropertyNames ?? [])}";
}
