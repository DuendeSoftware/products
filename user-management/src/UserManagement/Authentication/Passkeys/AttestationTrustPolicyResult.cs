// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys;

/// <summary>
/// Result of attestation trust policy evaluation.
/// </summary>
public abstract record AttestationTrustPolicyResult
{
    private AttestationTrustPolicyResult()
    {
    }

    /// <summary>
    /// The trust policy accepted the authenticator.
    /// </summary>
    public sealed record Accepted : AttestationTrustPolicyResult;

    /// <summary>
    /// Creates an accepted result.
    /// </summary>
    public static AttestationTrustPolicyResult Accept() => new Accepted();

    /// <summary>
    /// The trust policy rejected the authenticator.
    /// </summary>
    /// <param name="Reason">A human-readable description of the failure.</param>
    public sealed record Rejected(string Reason) : AttestationTrustPolicyResult;

    /// <summary>
    /// Creates a rejected result.
    /// </summary>
    /// <param name="reason">A human-readable description of the failure.</param>
    public static AttestationTrustPolicyResult Reject(string reason) => new Rejected(reason);
}
