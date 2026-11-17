// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Passkeys;

namespace PasskeyAuthentication.TrustPolicies;

internal sealed class ConditionalFidoMdsTrustPolicy(
    FidoMdsToggle toggle,
    FidoMdsTrustPolicy inner,
    ILogger<ConditionalFidoMdsTrustPolicy> logger)
    : IAttestationTrustPolicy
{
    public async ValueTask<AttestationTrustPolicyResult> EvaluateAsync(
        AttestationTrustContext context,
        Ct ct)
    {
        if (!toggle.Enabled)
        {
            logger.LogInformation("FIDO MDS trust policy is disabled via runtime toggle. Accepting attestation.");
            return AttestationTrustPolicyResult.Accept();
        }

        return await inner.EvaluateAsync(context, ct);
    }
}
