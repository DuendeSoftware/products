// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

public sealed class CheckSchemaVersionResult
{
    internal CheckSchemaVersionResult(uint currentVersion, uint requiredVersion)
    {
        CurrentVersion = currentVersion;
        RequiredVersion = requiredVersion;
    }

    public uint CurrentVersion { get; }

    public uint RequiredVersion { get; }

    public bool IsCompatible => CurrentVersion == RequiredVersion;
}
