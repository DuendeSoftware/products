// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Configuration.Profiles;

/// <summary>
/// Applies FAPI 2.0 Security Profile configuration to IdentityServerOptions.
/// When this profile is active, IdentityServer enforces FAPI 2.0 requirements.
/// </summary>
public class Fapi2ConfigurationProfile : IConfigurationProfile
{
    private readonly ILogger<Fapi2ConfigurationProfile> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Fapi2ConfigurationProfile"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public Fapi2ConfigurationProfile(ILogger<Fapi2ConfigurationProfile> logger) => _logger = logger;

    /// <inheritdoc />
    public string ProfileName => IdentityServerConstants.ConfigurationProfiles.Fapi2;

    private static ICollection<string> DefaultSigningAlgorithms = [
        SecurityAlgorithms.RsaSha256,
        SecurityAlgorithms.RsaSha384,
        SecurityAlgorithms.RsaSha512,
        SecurityAlgorithms.RsaSsaPssSha256,
        SecurityAlgorithms.RsaSsaPssSha384,
        SecurityAlgorithms.RsaSsaPssSha512,
        SecurityAlgorithms.EcdsaSha256,
        SecurityAlgorithms.EcdsaSha384,
        SecurityAlgorithms.EcdsaSha512
    ];

    /// <inheritdoc />
    public ProfileValidationResult ApplyProfile(IdentityServerOptions options)
    {
        // This profile enforces the rules in the FAPI Security Profile 2.0.
        // Most rules from the spec appear with the corresponding validation rule.
        // Rules that we cannot enforce are listed here, for completeness:
        //
        // 5.3.2.1 - General
        // Section 5.3.2.1 #07 (Prevent open redirectors)
        //      - Open redirectors are not a configuration problem. Redirect URLs are only matched exactly
        // Section 5.3.2.1 #10 (May use DPoP nonces)
        //      - Server nonces are optional in the spec
        // Section 5.3.2.1 #12 (Support Auth Code binding to DPoP key)
        //      - This is not configurable
        // Section 5.3.2.1 #14 (Least privilege)
        //      - This is advice for scope and authorization design, not configuration.

        // 5.3.2.2 - Authorization Endpoint
        // Section 5.3.2.2 #13 (Enough info on consent)
        //      - The consent screen is UI/user code
        //      - Unclear if disabling consent (especially for first party applications) is disallowed
        // Section 5.3.2.2 #04 (PAR requires authentication)
        //      - This is not configurable separately from the client requiring authentication
        // Section 5.3.2.2 #06 (PAR requires redirect_uri)
        //      - This is enforced by IdentityServer and cannot be disabled by config
        // Section 5.3.2.2 #09 (No reuse of authorization codes)
        //      - This is also enforced by IdentityServer and cannot be disabled by config
        // Section 5.3.2.2 #10 (HTTP 307 disallowed)
        // Section 5.3.2.2 #11 (HTTP 303 encouraged)
        //      - IdentityServer uses 302, and that is not configurable. HTTP 303 is not mandatory.


        var result = new ProfileValidationResult();
        var validator = new IdentityServerOptionsProfileValidator(options, _logger, options.ConfigurationProfiles.LogProfileOverrides);


        // Section 5.3.2.1 #01 (Use discovery)
        validator.EndpointsEnableDiscoveryEndpoint()
            .HasDefault(true)
            .ViolatesIf(value => value == false)
            .OverrideWith(true)
            .Validate(result);

        // Section 5.3.2.1 #08 (Only accept our issuer as a string for aud in client assertions)
        validator.PreviewStrictClientAssertionAudienceValidation()
            .HasDefault(false)
            .ViolatesIf(value => value == false)
            .OverrideWith(true)
            .Validate(result);

        // Section 5.3.2.1 #13 (10-60 seconds of clock skew)
        validator.JwtValidationClockSkew()
            .HasDefault(TimeSpan.FromMinutes(5))
            .ViolatesIf(value => value < TimeSpan.FromSeconds(10) || value > TimeSpan.FromSeconds(60))
            .OverrideWith(TimeSpan.FromSeconds(10))
            .Validate(result);

        // Section 5.3.2.2 #02 (PAR is supported)
        validator.EndpointsEnablePushedAuthorizationEndpoint()
            .HasDefault(true)
            .ViolatesIf(value => value == false)
            .OverrideWith(true)
            .Validate(result);

        // Section 5.3.2.2 #03 (PAR is required)
        validator.PushedAuthorizationRequired()
            .HasDefault(false)
            .ViolatesIf(value => value == false)
            .OverrideWith(true)
            .Validate(result);

        // Section 5.3.2.2 #07 (Include iss in authorize responses)
        validator.EmitIssuerIdentificationResponseParameter()
            .HasDefault(true)
            .ViolatesIf(value => value == false)
            .OverrideWith(true)
            .Validate(result);

        // Section 5.3.2.2 #12 (PAR request uris expire after 10 minutes)
        validator.PushedAuthorizationLifetime()
            .HasDefault(600)
            .ViolatesIf(value => value > 600)
            .OverrideWith(600)
            .Validate(result);

        // Section 5.3.2.2 #14 (OIDC nonces up to 64 characters in length must be allowed)
        validator.InputLengthRestrictionsNonce()
            .HasDefault(300)
            .ViolatesIf(value => value < 64)
            .OverrideWith(64)
            .Validate(result);

        // Section 5.4.1 #2 (Use PS256, ES256, or EdDSA algorithms)
        validator.KeyManagementSigningAlgorithms()
            .HasDefault([]) // KeyManagement is the exception that doesn't use the default signing algorithms defined above
            .ViolatesIf(value =>
                value.Count == 0 ||
                value.Any(alg =>
                    alg.Name != SecurityAlgorithms.RsaSsaPssSha256 &&
                    alg.Name != SecurityAlgorithms.EcdsaSha256))
            .OverrideWith([new SigningAlgorithmOptions(SecurityAlgorithms.RsaSsaPssSha256)])
            .Validate(result);

        // Section 5.4.1 #2 (Use PS256, ES256, or EdDSA algorithms)
        validator.DPoPSupportedDPoPSigningAlgorithms()
            .HasDefault(DefaultSigningAlgorithms)
            .ViolatesIf(value =>
                value.Count == 0 ||
                value.Any(alg =>
                    alg != SecurityAlgorithms.RsaSsaPssSha256 &&
                    alg != SecurityAlgorithms.EcdsaSha256))
            .OverrideWith([SecurityAlgorithms.RsaSsaPssSha256, SecurityAlgorithms.EcdsaSha256])
            .Validate(result);

        // Section 5.4.1 #2 (Use PS256, ES256, or EdDSA algorithms)
        validator.SupportedClientAssertionSigningAlgorithms()
            .HasDefault(DefaultSigningAlgorithms)
            .ViolatesIf(value =>
                value.Count == 0 ||
                value.Any(alg =>
                    alg != SecurityAlgorithms.RsaSsaPssSha256 &&
                    alg != SecurityAlgorithms.EcdsaSha256))
            .OverrideWith([SecurityAlgorithms.RsaSsaPssSha256, SecurityAlgorithms.EcdsaSha256])
            .Validate(result);

        // Section 5.4.1 #2 (Use PS256, ES256, or EdDSA algorithms)
        validator.SupportedRequestObjectSigningAlgorithms()
            .HasDefault(DefaultSigningAlgorithms)
            .ViolatesIf(value =>
                value.Count == 0 ||
                value.Any(alg =>
                    alg != SecurityAlgorithms.RsaSsaPssSha256 &&
                    alg != SecurityAlgorithms.EcdsaSha256))
            .OverrideWith([SecurityAlgorithms.RsaSsaPssSha256, SecurityAlgorithms.EcdsaSha256])
            .Validate(result);

        return result;
    }

    /// <inheritdoc />
    public ProfileValidationResult ValidateClient(IdentityServerOptions options, Validation.ClientConfigurationValidationContext context)
    {

        var result = new ProfileValidationResult();
        var client = context.Client;
        var builder = new ClientProfileValidator(client, _logger, options.ConfigurationProfiles.LogProfileOverrides);

        // Section 5.3.2.1 #02 (Password grant forbidden)
        // Section 5.3.2.2 #01 (response_type must be code - forbids implicit grant and hybrid flow)
        builder.AllowedGrantTypes()
            .HasDefault([])
            .ViolatesIf(value => value.Contains(GrantType.ResourceOwnerPassword) ||
                value.Contains(GrantType.Implicit) ||
                value.Contains(GrantType.Hybrid)
            )
            .WarnWith("FAPI 2.0 Security Profile forbids use of the hybrid flow, resource owner password grant, or implicit grant.")
            .Validate(result);

        // Section 5.3.2.1 #11 (Code lifetime max of 60 seconds)
        builder.AuthorizationCodeLifetime()
            .HasDefault(300)
            .ViolatesIf(value => value > 60)
            .OverrideWith(60)
            .Validate(result);

        // Section 5.3.2.2 #05 (PKCE is required)
        builder.RequirePkce()
            .HasDefault(true)
            .ViolatesIf(value => value == false)
            .OverrideWith(true)
            .Validate(result);

        // AllowedIdentityTokenSigningAlgorithms should either be null or only contain PS256 or ES256
        builder.AllowedIdentityTokenSigningAlgorithms()
            .HasDefault([])
            .ViolatesIf(algs =>
                algs.Any(alg =>
                    // Unlike most other algorithm checks, we allow an empty collection here. If empty, IdentityServer will use the server default signing algorithm, which is validated separately.
                    alg != SecurityAlgorithms.RsaSsaPssSha256 &&
                    alg != SecurityAlgorithms.EcdsaSha256))
            .WarnWith("FAPI 2.0 Security Profile requires Signing Algorithms to only use PS256 or ES256.")
            .Validate(result);

        // Section 5.3.2.1 #03 (Only confidential clients are allowed - must have secret)
        // Section 5.3.2.1 #06 (Authenticate with mtls or private_key_jwt)
        builder.ClientSecrets()
            .HasDefault([])
            .ViolatesIf(secrets =>
                secrets.Count == 0 ||
                secrets.Any(secret =>
                    secret.Type != IdentityServerConstants.SecretTypes.X509CertificateThumbprint &&
                    secret.Type != IdentityServerConstants.SecretTypes.X509CertificateName &&
                    secret.Type != IdentityServerConstants.SecretTypes.X509CertificateBase64 &&
                    secret.Type != IdentityServerConstants.SecretTypes.JsonWebKey))
            .WarnWith("FAPI 2.0 Security Profile requires client authentication using either mTLS (X.509 certificate) or client assertions (JWT).")
            .Validate(result);

        // Section 5.3.2.1 #04 (Access tokens are sender constrained)
        // Section 5.3.2.1 #05 (mTLS or DPoP are allowed for sender constraining)
        builder.RequireDPoP()
            .HasDefault(false)
            .ViolatesIf(requireDPoP => !(options.MutualTls.Enabled && (options.MutualTls.AlwaysEmitConfirmationClaim || UsesMtlsAuthentication(client))) &&
                                       !requireDPoP)
            .WarnWith("FAPI 2.0 Security Profile requires sender-constraining of tokens using either mTLS (X.509 certificate) or DPoP (Demonstration of Proof-of-Possession).")
            .Validate(result);

        // Section 5.3.2.1 #09 (Don't use refresh token rotation)
        // While there are "extraordinary circumstances" where the security profile allows rotation, for simplicity this profile disallows it.
        builder.RefreshTokenUsage()
            .HasDefault(TokenUsage.ReUse)
            .ViolatesIf(value => value != TokenUsage.ReUse)
            .OverrideWith(TokenUsage.ReUse)
            .Validate(result);

        // Section 5.3.2.2 #08 (No authorization response over unencrypted network connections. Must use https (AppAuth can use loopback))
        builder.RedirectUris()
            .HasDefault([])
            .ViolatesIf(uris => uris.Any(configuredUri =>
            {
                var uri = new Uri(configuredUri);
                return uri.Scheme != "https" && uri.Host is not "localhost" and not "127.0.0.1";
            }))
            .WarnWith("FAPI 2.0 Security Profile requires redirect URIs to use https, except for native clients using the loopback interface.")
            .Validate(result);

        return result;

        static bool UsesMtlsAuthentication(Client client) =>
                    client.ClientSecrets.Count > 0 &&
                    client.ClientSecrets.All(secret =>
                        secret.Type == IdentityServerConstants.SecretTypes.X509CertificateThumbprint ||
                        secret.Type == IdentityServerConstants.SecretTypes.X509CertificateName ||
                        secret.Type == IdentityServerConstants.SecretTypes.X509CertificateBase64);
    }
}
