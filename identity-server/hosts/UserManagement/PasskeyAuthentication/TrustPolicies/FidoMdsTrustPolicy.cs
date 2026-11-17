// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.UserManagement.Authentication.Passkeys;

namespace PasskeyAuthentication.TrustPolicies;

public sealed class FidoMdsTrustPolicy(
    FidoMdsCache cache,
    ILogger<FidoMdsTrustPolicy> logger)
    : IAttestationTrustPolicy
{
    private readonly FidoMdsCache _fidoMetadata = cache ?? throw new ArgumentNullException(nameof(cache));

    /// <summary>
    /// <see cref="CertificationLevel"/>
    /// </summary>
    private const int MinimumCertificationLevel = 3; //L2

    private readonly ILogger<FidoMdsTrustPolicy> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async ValueTask<AttestationTrustPolicyResult> EvaluateAsync(
        AttestationTrustContext context,
        Ct ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.AttestationFormat != PasskeyConstants.AttestationFormat.None &&
            context.CertificateChain is not { Count: > 0 })
        {
            _logger.LogInformation(
                "Attestation for AAGUID {Aaguid} has no certificate chain (format: {Format}). " +
                "Cannot verify the AAGUID is authentic. Registration is denied. " +
                "If you want to allow registration, comment / remove the call to register the AddAttestationTrustPolicy<{FidoMdsTrustPolicy}>.",
                context.Aaguid, context.AttestationFormat, nameof(FidoMdsTrustPolicy));
            return AttestationTrustPolicyResult.Reject(
                "Attestation does not include a certificate chain, so the AAGUID cannot be verified.");
        }

        try
        {
            var entries = await _fidoMetadata.GetEntriesAsync(ct);
            if (!entries.TryGetValue(context.Aaguid, out var authenticatorCertificationLevel))
            {
                _logger.LogInformation(
                    "The Authenticator with `{Aaguid}` was not found in the Metadata Service. Registration is denied. " +
                    "If you want to allow registration, comment / remove the call to register the AddAttestationTrustPolicy<{FidoMdsTrustPolicy}>.",
                    context.Aaguid, nameof(FidoMdsTrustPolicy));
                return AttestationTrustPolicyResult.Reject(
                    $"Authenticator AAGUID '{context.Aaguid}' is not registered in the FIDO Metadata Service.");
            }

            if (authenticatorCertificationLevel is null or < MinimumCertificationLevel)
            {
                _logger.LogInformation(
                    "The Authenticator with `{Aaguid}` with certification level of {AuthenticatorCertificationLevel} did not meet the minimum required certification level of {MinimumCertificationLevel}. Registration is denied." +
                    "If you want to allow registration, comment / remove the call to register the AddAttestationTrustPolicy<{FidoMdsTrustPolicy}>.",
                    context.Aaguid, authenticatorCertificationLevel, MinimumCertificationLevel,
                    nameof(FidoMdsTrustPolicy));
                return AttestationTrustPolicyResult.Reject(
                    $"Authenticator AAGUID '{context.Aaguid}' does not meet the minimum FIDO certification level '{MinimumCertificationLevel}'.");
            }

            return AttestationTrustPolicyResult.Accept();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException
                                       or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to evaluate AAGUID {Aaguid} against the FIDO Metadata Service.",
                context.Aaguid);
            return AttestationTrustPolicyResult.Reject(
                "Unable to validate the authenticator against the FIDO Metadata Service.");
        }
    }
}
