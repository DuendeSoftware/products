// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.UserManagement.Authentication.Passkeys.Internal.Storage;

/// <summary>
/// Data store object for passkey registration challenges.
/// </summary>
internal static class PasskeyRegistrationChallengeDso
{
    internal static readonly EntityType EntityType = new(1002, "PasskeyRegistrationChallengeDso");

    /// <summary>
    /// Version 1 of the passkey registration challenge storage schema.
    /// </summary>
    /// <param name="Id">Primary key (also used as session ID).</param>
    /// <param name="Challenge">Cryptographic challenge bytes (Base64Url encoded).</param>
    /// <param name="UserSubjectId">Subject ID of the user registering the passkey.</param>
    /// <param name="CreatedAt">When the challenge was created.</param>
    internal sealed record V1(
        Guid Id,
        string Challenge,
        string UserSubjectId,
        DateTimeOffset CreatedAt) : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
    }
}
