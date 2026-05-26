// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Evaluates whether an authenticator should be trusted during registration.
/// Implement this interface to enforce authenticator trust decisions
/// (e.g., AAGUID allowlists, FIDO MDS-based policies).
/// </summary>
public interface IAttestationTrustPolicy
{
    /// <summary>
    /// Evaluates the trust policy against the given attestation context.
    /// </summary>
    ValueTask<AttestationTrustPolicyResult> EvaluateAsync(AttestationTrustContext context, Ct ct);
}
