// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Information about the desired properties of the credential to be created.
/// </summary>
public sealed record PublicKeyCredentialParameters
{
    /// <summary>
    /// The type of credential to create. Always "public-key".
    /// </summary>
    public string Type { get; init; } = PasskeyConstants.CredentialType.PublicKey;

    /// <summary>
    /// The COSE algorithm identifier.
    /// </summary>
    public required int Alg { get; init; }
}
