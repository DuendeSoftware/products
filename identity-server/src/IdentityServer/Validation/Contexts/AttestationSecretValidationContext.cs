// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Validation;

public class AttestationSecretValidationContext
{
    /// <summary>
    /// Client identifier to validate.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Client Attestation JWT to validate.
    /// </summary>
    public required string ClientAttestationJwt { get; init; }

    /// <summary>
    /// Client Attestation Proof of Possession JWT to validate.
    /// </summary>
    public required string ClientAttestationPopJwt { get; init; }
}
