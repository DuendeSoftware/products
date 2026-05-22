// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage;

/// <summary>
/// Represents the result of a database schema version check.
/// </summary>
public sealed class CheckSchemaVersionResult
{
    internal CheckSchemaVersionResult(uint currentVersion, uint requiredVersion)
    {
        CurrentVersion = currentVersion;
        RequiredVersion = requiredVersion;
    }

    /// <summary>
    /// Gets the current schema version in the database.
    /// </summary>
    public uint CurrentVersion { get; }

    /// <summary>
    /// Gets the schema version required by the application.
    /// </summary>
    public uint RequiredVersion { get; }

    /// <summary>
    /// Gets a value indicating whether the current schema version is compatible with the required version.
    /// </summary>
    public bool IsCompatible => CurrentVersion == RequiredVersion;
}
