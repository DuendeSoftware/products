// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Services;
using Duende.Spaces;
using Microsoft.Data.Sqlite;

namespace Duende.IdentityServer.IntegrationTests.Hosting;

/// <summary>
/// Which store a <see cref="SpacesDataProtectionTestHost"/> uses for its signing keys.
/// </summary>
public enum SpacesSigningKeyStore
{
    /// <summary>Key management is off and a developer signing credential is used (the default).</summary>
    DeveloperCredential,

    /// <summary>Key management is on and keys are stored by <c>FileSystemKeyStore</c>.</summary>
    FileSystem,

    /// <summary>Key management is on and keys are stored in the operational storage.</summary>
    Storage
}

/// <summary>
/// A key ring directory and application name that several hosts share, so a value protected by one host can
/// be unprotected by another. Hosts register it before <c>AddSpaces()</c>.
/// </summary>
internal sealed class SpacesTestKeyRing : IDisposable
{
    public SpacesTestKeyRing()
    {
        DirectoryPath = Path.Combine(Path.GetTempPath(), $"spaces-dp-keys-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(DirectoryPath);
        ApplicationName = $"spaces-dp-{Guid.NewGuid():N}";
    }

    public string DirectoryPath { get; }
    public string ApplicationName { get; }

    public void Dispose()
    {
        if (Directory.Exists(DirectoryPath))
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}

/// <summary>
/// An in-memory SQLite database and a signing key directory that hosts can share, so data written by one host
/// is visible to the next. A kept-open connection holds the database alive between hosts. The first host
/// creates the spaces; later hosts reuse them.
/// </summary>
internal sealed class SpacesTestSharedStorage : IDisposable
{
    private readonly SqliteConnection _keepAlive;

    public SpacesTestSharedStorage()
    {
        DatabaseName = $"msdp_{Guid.NewGuid():N}";
        _keepAlive = new SqliteConnection($"Data Source={DatabaseName};Mode=Memory;Cache=Shared");
        _keepAlive.Open();

        SigningKeyDirectory = Path.Combine(Path.GetTempPath(), $"spaces-signing-keys-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(SigningKeyDirectory);
    }

    public string DatabaseName { get; }
    public string SigningKeyDirectory { get; }

    internal SpaceId? SpaceAId { get; set; }
    internal SpaceId? SpaceBId { get; set; }

    public void Dispose()
    {
        _keepAlive.Dispose();
        if (Directory.Exists(SigningKeyDirectory))
        {
            Directory.Delete(SigningKeyDirectory, recursive: true);
        }
    }
}

/// <summary>
/// Options of a <see cref="SpacesDataProtectionTestHost"/>. The defaults give a host with Spaces, partitioned
/// data protection, the ticket in the cookie and a developer signing credential.
/// </summary>
internal sealed record SpacesDataProtectionTestHostOptions
{
    /// <summary>When set, the host registers this key ring before <c>AddSpaces()</c>.</summary>
    public SpacesTestKeyRing? KeyRing { get; init; }

    /// <summary>When set, the host uses this database and signing key directory and reuses its spaces.</summary>
    public SpacesTestSharedStorage? SharedStorage { get; init; }

    /// <summary>When <c>false</c>, the host has no Spaces: no <c>AddSpaces()</c>, no space resolution, no spaces.</summary>
    public bool EnableSpaces { get; init; } = true;

    /// <summary>Sets <c>SpacesOptions.PartitionDataProtection</c>.</summary>
    public bool PartitionDataProtection { get; init; } = true;

    /// <summary>Sets <c>SpacesOptions.FallbackToDefault</c>.</summary>
    public bool FallbackToDefault { get; init; }

    /// <summary>
    /// When <c>true</c>, the authentication ticket is stored by the operational storage and the background
    /// outbox processor is disabled (see <see cref="SpacesDataProtectionTestHost.RunOutboxProcessorAsync"/>).
    /// </summary>
    public bool ServerSideSessions { get; init; }

    public SpacesSigningKeyStore SigningKeyStore { get; init; } = SpacesSigningKeyStore.DeveloperCredential;

    /// <summary>
    /// When set, replaces the back-channel logout service and makes expired server-side sessions trigger
    /// back-channel logout.
    /// </summary>
    public IBackChannelLogoutService? BackChannelLogoutService { get; init; }
}
