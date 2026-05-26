// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.UserManagement.Authentication.Internal.Storage;

namespace Duende.UserManagement.Authentication.Passkeys.Internal.Storage;

/// <summary>
/// Secondary key for looking up a user by passkey credential ID.
/// Enables discoverable credential authentication.
/// </summary>
internal sealed record PasskeyCredentialIdDskV1 : IDataStorageKey
{
    private PasskeyCredentialIdDskV1(string credentialId) => CredentialId = credentialId;

    public static DataStorageKeyVersion DskVersion { get; } =
        new(UserAuthenticatorsRepository.Keys.PasskeyCredentialId, 1);

    public string CredentialId { get; }

    /// <summary>
    /// Creates a DSK from a WebAuthn credential ID, storing the full base64-encoded
    /// credential ID for exact-match lookup.
    /// </summary>
    public static PasskeyCredentialIdDskV1 Create(PasskeyCredentialId credentialId) =>
        new(credentialId.ToBase64String());
}
