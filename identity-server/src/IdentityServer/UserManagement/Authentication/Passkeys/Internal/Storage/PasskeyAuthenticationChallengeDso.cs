// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.Internal;

namespace Duende.UserManagement.Authentication.Passkeys.Internal.Storage;

/// <summary>
/// Data store object for passkey authentication challenges.
/// </summary>
internal static class PasskeyAuthenticationChallengeDso
{
    internal static readonly EntityType EntityType = new(1003, "PasskeyAuthenticationChallengeDso");

    /// <summary>
    /// Converts a stored PasskeyAuthenticationChallengeDso of any registered version to the current version (V1).
    /// A future V2 adds an arm here that upconverts older versions.
    /// </summary>
    internal static V1 ToCurrent(IDataStorageObject dso) => dso switch
    {
        V1 v1 => v1,
        _ => throw new InvalidOperationException($"Unexpected {nameof(PasskeyAuthenticationChallengeDso)} type {dso.GetType()}.")
    };

    /// <summary>
    /// Version 1 of the passkey authentication challenge storage schema.
    /// </summary>
    /// <param name="Id">Primary key (also used as session ID).</param>
    /// <param name="Challenge">Cryptographic challenge bytes (Base64Url encoded).</param>
    /// <param name="UserSubjectId">Subject ID of the user authenticating with their passkey (null for discoverable credentials).</param>
    /// <param name="CreatedAt">When the challenge was created.</param>
    internal sealed record V1(
        Guid Id,
        string Challenge,
        string? UserSubjectId,
        DateTimeOffset CreatedAt) : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
    }
}
