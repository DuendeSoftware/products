// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Represents an error that occurred during a schema admin operation.
/// </summary>
/// <param name="Code">A machine-readable error code.</param>
/// <param name="Message">A human-readable error description.</param>
public sealed record SchemaError(string Code, string Message)
{
    /// <summary>Creates an error indicating that the schema already exists.</summary>
    public static SchemaError AlreadyExists(string schemaId) =>
        new("already_exists", $"A schema with identifier '{schemaId}' already exists.");

    /// <summary>Creates an error indicating that the schema was not found.</summary>
    public static SchemaError NotFound(string schemaId) =>
        new("not_found", $"The schema with identifier '{schemaId}' was not found.");

    /// <summary>Creates an error indicating that a version conflict occurred.</summary>
    public static SchemaError VersionConflict() =>
        new("version_conflict", "The schema has been modified. Refresh and retry.");

    /// <summary>Creates an error indicating that validation failed.</summary>
    public static SchemaError ValidationFailed(string message) =>
        new("validation_failed", message);

    /// <inheritdoc/>
    public override string ToString() => $"{Code}: {Message}";
}

/// <summary>
///     Represents the result of a schema save (create or update) operation.
/// </summary>
public sealed record SchemaSaveResult
{
    /// <summary>
    ///     Gets a value indicating whether the operation succeeded.
    ///     When <see langword="true"/>, <see cref="Version"/> is set.
    ///     When <see langword="false"/>, <see cref="Errors"/> is set.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Version))]
    [MemberNotNullWhen(false, nameof(Errors))]
    public bool IsSuccess { get; init; }

    /// <summary>
    ///     The schema ID of the saved schema, or <see langword="null"/> if the operation failed.
    /// </summary>
    public SchemaId? SchemaId { get; init; }

    /// <summary>
    ///     The version assigned after the save, or <see langword="null"/> if the operation failed.
    /// </summary>
    public int? Version { get; init; }

    /// <summary>
    ///     Errors that caused the failure, or <see langword="null"/> if the operation succeeded.
    /// </summary>
    public IReadOnlyList<SchemaError>? Errors { get; init; }

    /// <summary>Creates a success result.</summary>
    public static SchemaSaveResult Success(SchemaId schemaId, int version) =>
        new() { IsSuccess = true, SchemaId = schemaId, Version = version };

    /// <summary>Creates a failure result.</summary>
    public static SchemaSaveResult Failure(params SchemaError[] errors) =>
        new() { IsSuccess = false, Errors = errors };
}

/// <summary>
///     Represents the result of a schema get operation.
/// </summary>
public sealed record SchemaGetResult
{
    /// <summary>
    ///     Gets a value indicating whether the schema was found.
    ///     When <see langword="true"/>, <see cref="Schema"/> and <see cref="Version"/> are set.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Schema), nameof(Version))]
    public bool Found { get; init; }

    /// <summary>
    ///     The retrieved schema configuration, or <see langword="null"/> if not found.
    /// </summary>
    public SchemaConfiguration? Schema { get; init; }

    /// <summary>
    ///     The version of the retrieved schema, or <see langword="null"/> if not found.
    /// </summary>
    public int? Version { get; init; }

    /// <summary>Creates a found result.</summary>
    public static SchemaGetResult Ok(SchemaConfiguration schema, int version) =>
        new() { Found = true, Schema = schema, Version = version };

    /// <summary>Creates a not-found result.</summary>
    public static SchemaGetResult NotFound() =>
        new() { Found = false };
}

/// <summary>
///     Represents the result of a schema query operation.
/// </summary>
public sealed record SchemaQueryResult
{
    /// <summary>
    ///     The schemas returned by the query.
    /// </summary>
    public IReadOnlyList<SchemaSummary> Results { get; init; } = [];

    /// <summary>
    ///     The total count of schemas matching the query (if available).
    /// </summary>
    public int? TotalCount { get; init; }

    /// <summary>Creates a query result.</summary>
    public static SchemaQueryResult Ok(IReadOnlyList<SchemaSummary> results, int? totalCount = null) =>
        new() { Results = results, TotalCount = totalCount };
}
