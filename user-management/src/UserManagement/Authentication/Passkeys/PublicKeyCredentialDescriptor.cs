// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json.Serialization;

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Describes a credential to be used for authentication.
/// https://www.w3.org/TR/webauthn-3/#dictdef-publickeycredentialdescriptor
/// </summary>
public sealed record PublicKeyCredentialDescriptor
{
    /// <summary>
    /// The credential type. Must be "public-key".
    /// </summary>
    public string Type { get; init; } = PasskeyConstants.CredentialType.PublicKey;

    /// <summary>
    /// The credential ID (base64url-encoded).
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Optional transport hints used by the browser to locate this credential.
    /// These values do not restrict which transports may authenticate.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Transports { get; init; }
}
