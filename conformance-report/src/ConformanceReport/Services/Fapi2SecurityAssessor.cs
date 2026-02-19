// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.ConformanceReport.Models;

namespace Duende.ConformanceReport.Services;

/// <summary>
/// Assesses configuration against the FAPI 2.0 Security Profile specification.
/// See: https://openid.net/specs/fapi-security-profile-2_0-final.html
/// </summary>
internal class Fapi2SecurityAssessor(ConformanceReportServerOptions options)
{
    // FAPI 2.0 requires asymmetric algorithms only, PS256 or ES256 recommended
    private static readonly HashSet<string> Fapi2AllowedAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        SigningAlgorithms.RsaSsaPssSha256,
        SigningAlgorithms.RsaSsaPssSha384,
        SigningAlgorithms.RsaSsaPssSha512,
        SigningAlgorithms.EcdsaSha256,
        SigningAlgorithms.EcdsaSha384,
        SigningAlgorithms.EcdsaSha512,
        "PS256",
        "PS384",
        "PS512",
        "ES256",
        "ES384",
        "ES512"
    };

    // Maximum PAR lifetime recommended by FAPI 2.0 (10 minutes)
    private const int MaxParLifetimeSeconds = 600;

    // Maximum authorization code lifetime for FAPI 2.0
    private const int MaxAuthCodeLifetimeSeconds = 60;

    /// <summary>
    /// Assesses server-level configuration against FAPI 2.0 Security Profile requirements.
    /// </summary>
    public IReadOnlyList<Finding> AssessServer()
    {
        var findings = new List<Finding>
        {
            // FS01: PAR must be supported and required
            AssessParRequired(),
            // FS02: Sender-constrained tokens must be required
            AssessSenderConstrainedTokensRequired(),
            // FS03: Only PS256 or ES256 signing algorithms
            AssessSigningAlgorithms(),
            // FS04: PAR lifetime should be 10 minutes or less
            AssessParLifetime(),
            // FS05: mTLS or DPoP must be supported
            AssessSenderConstrainedTokenSupport(),
            // FS06: Issuer identification response parameter required
            AssessIssuerIdentification(),
            // FS07: HTTP 303 redirects required
            AssessHttp303Redirects(),
            // FS08: PKCE is required (checked at client level, note server support)
            new()
            {
                RuleId = "FS08",
                RuleName = "PKCE Support",
                Status = FindingStatus.Pass,
                Message = "Server supports PKCE. Individual client PKCE requirements are assessed separately."
            }
        };

        return findings;
    }

    /// <summary>
    /// Assesses a client's configuration against FAPI 2.0 Security Profile requirements.
    /// </summary>
    public IReadOnlyList<Finding> AssessClient(ConformanceReportClient client)
    {
        var findings = new List<Finding>
        {
            // FC01: Only authorization_code grant allowed (no implicit, hybrid, password)
            AssessGrantType(client),
            // FC02: Must be confidential client
            AssessConfidentialClient(client),
            // FC03: PKCE required with S256
            AssessPkceS256(client),
            // FC04: PAR must be required
            AssessClientParRequired(client),
            // FC05: Sender-constrained tokens required (DPoP or mTLS)
            AssessSenderConstrainedTokens(client),
            // FC06: Private key JWT or mTLS for client authentication
            AssessClientAuthentication(client),
            // FC07: Authorization code lifetime <= 60 seconds
            AssessAuthCodeLifetime(client),
            // FC08: Refresh token rotation required if refresh tokens enabled
            AssessRefreshTokenRotation(client),
            // FC09: DPoP nonce required if using DPoP
            AssessDPoPNonce(client),
            // FC10: Explicit redirect URIs (no wildcards)
            AssessExplicitRedirectUris(client),
            // FC11: No access tokens via browser
            AssessNoAccessTokensViaBrowser(client),
            // FC12: Request object required (for highest security)
            AssessRequestObject(client)
        };

        return findings;
    }

    private Finding AssessParRequired()
    {
        var parEnabled = options.PushedAuthorizationEndpointEnabled;
        var parRequired = options.PushedAuthorizationRequired;

        if (!parEnabled)
        {
            return new Finding
            {
                RuleId = "FS01",
                RuleName = "PAR Endpoint",
                Status = FindingStatus.Fail,
                Message = "PAR endpoint is not enabled. FAPI 2.0 requires PAR.",
                Recommendation = "Enable the PAR endpoint and require PAR globally or per-client."
            };
        }

        return new Finding
        {
            RuleId = "FS01",
            RuleName = "PAR Endpoint",
            Status = parRequired ? FindingStatus.Pass : FindingStatus.Warning,
            Message = parRequired
                ? "PAR endpoint is enabled and required globally."
                : "PAR endpoint is enabled but not required globally. FAPI 2.0 requires PAR for all authorization requests.",
            Recommendation = parRequired ? null : "Set PushedAuthorization.Required = true or require PAR per-client."
        };
    }

    private Finding AssessSenderConstrainedTokensRequired()
    {
        var mtlsEnabled = options.MutualTlsEnabled;

        return new Finding
        {
            RuleId = "FS02",
            RuleName = "Sender-Constrained Token Requirement",
            Status = mtlsEnabled ? FindingStatus.Pass : FindingStatus.Warning,
            Message = mtlsEnabled
                ? "mTLS is enabled at the server level for sender-constrained tokens."
                : "mTLS is not enabled at the server level. DPoP can be configured per-client. FAPI 2.0 requires sender-constrained tokens.",
            Recommendation = mtlsEnabled ? null : "Enable mTLS or ensure all clients require DPoP."
        };
    }

    private Finding AssessSigningAlgorithms()
    {
        var algorithms = options.SupportedSigningAlgorithms;
        var nonFapiAlgorithms = algorithms
            .Where(a => !Fapi2AllowedAlgorithms.Contains(a))
            .ToList();

        if (nonFapiAlgorithms.Count == 0)
        {
            return new Finding
            {
                RuleId = "FS03",
                RuleName = "FAPI 2.0 Signing Algorithms",
                Status = FindingStatus.Pass,
                Message = "All signing algorithms are FAPI 2.0 compliant (PS256/384/512 or ES256/384/512)."
            };
        }

        // Check if at least FAPI-compliant algorithms are present
        var hasFapiAlgorithm = algorithms.Any(a => Fapi2AllowedAlgorithms.Contains(a));

        return new Finding
        {
            RuleId = "FS03",
            RuleName = "FAPI 2.0 Signing Algorithms",
            Status = hasFapiAlgorithm ? FindingStatus.Warning : FindingStatus.Fail,
            Message = hasFapiAlgorithm
                ? $"Non-FAPI 2.0 algorithms are configured: {string.Join(", ", nonFapiAlgorithms)}. FAPI 2.0 recommends PS256 or ES256 only."
                : $"No FAPI 2.0 compliant algorithms configured. Found: {string.Join(", ", algorithms)}.",
            Recommendation = "Use only PS256, PS384, PS512, ES256, ES384, or ES512 algorithms for FAPI 2.0 conformance."
        };
    }

    private Finding AssessParLifetime()
    {
        var lifetime = options.PushedAuthorizationLifetime;

        if (lifetime <= MaxParLifetimeSeconds)
        {
            return new Finding
            {
                RuleId = "FS04",
                RuleName = "PAR Lifetime",
                Status = FindingStatus.Pass,
                Message = $"PAR lifetime is {lifetime} seconds, within the FAPI 2.0 recommended maximum of {MaxParLifetimeSeconds} seconds."
            };
        }

        return new Finding
        {
            RuleId = "FS04",
            RuleName = "PAR Lifetime",
            Status = FindingStatus.Fail,
            Message = $"PAR lifetime is {lifetime} seconds. FAPI 2.0 recommends a maximum of {MaxParLifetimeSeconds} seconds (10 minutes).",
            Recommendation = $"Set PushedAuthorization.Lifetime to {MaxParLifetimeSeconds} or less."
        };
    }

    private Finding AssessSenderConstrainedTokenSupport()
    {
        var mtlsEnabled = options.MutualTlsEnabled;
        // DPoP is always available per-client

        if (mtlsEnabled)
        {
            return new Finding
            {
                RuleId = "FS05",
                RuleName = "Sender-Constrained Token Mechanisms",
                Status = FindingStatus.Pass,
                Message = "mTLS is enabled. DPoP is also available per-client configuration."
            };
        }

        return new Finding
        {
            RuleId = "FS05",
            RuleName = "Sender-Constrained Token Mechanisms",
            Status = FindingStatus.Pass,
            Message = "DPoP is available for sender-constrained tokens via per-client configuration. mTLS is not enabled at server level."
        };
    }

    private Finding AssessIssuerIdentification() =>
        new()
        {
            RuleId = "FS06",
            RuleName = "Issuer Identification",
            Status = options.EmitIssuerIdentificationResponseParameter ? FindingStatus.Pass : FindingStatus.Fail,
            Message = options.EmitIssuerIdentificationResponseParameter
                ? "Issuer identification response parameter (iss) is enabled."
                : "Issuer identification response parameter is not enabled. FAPI 2.0 requires this for mix-up attack prevention.",
            Recommendation = options.EmitIssuerIdentificationResponseParameter
                ? null
                : "Set EmitIssuerIdentificationResponseParameter = true."
        };

    private Finding AssessHttp303Redirects() =>
        new()
        {
            RuleId = "FS07",
            RuleName = "HTTP 303 Redirects",
            Status = options.UseHttp303Redirects ? FindingStatus.Pass : FindingStatus.Fail,
            Message = options.UseHttp303Redirects
                ? "HTTP 303 (See Other) redirects are enabled as required by FAPI 2.0."
                : "HTTP 302 (Found) redirects are used. FAPI 2.0 Section 5.3.2.2 requires HTTP 303 to prevent POST data resubmission.",
            Recommendation = options.UseHttp303Redirects ? null : "Set UseHttp303Redirects = true."
        };

    private static Finding AssessGrantType(ConformanceReportClient client)
    {
        // FAPI 2.0 only allows authorization_code (and client_credentials for service accounts)
        var allowedGrants = new HashSet<string>
        {
            ConformanceReportGrantTypes.AuthorizationCode,
            ConformanceReportGrantTypes.ClientCredentials,
            ConformanceReportGrantTypes.RefreshToken
        };

        var disallowedGrants = client.AllowedGrantTypes
            .Where(g => !allowedGrants.Contains(g))
            .ToList();

        if (disallowedGrants.Count == 0)
        {
            return new Finding
            {
                RuleId = "FC01",
                RuleName = "FAPI 2.0 Grant Types",
                Status = FindingStatus.Pass,
                Message = $"Client uses FAPI 2.0 compliant grant types: {string.Join(", ", client.AllowedGrantTypes)}."
            };
        }

        return new Finding
        {
            RuleId = "FC01",
            RuleName = "FAPI 2.0 Grant Types",
            Status = FindingStatus.Fail,
            Message = $"Client uses non-FAPI 2.0 grant types: {string.Join(", ", disallowedGrants)}. FAPI 2.0 only allows authorization_code and client_credentials.",
            Recommendation = "Remove implicit, hybrid, password, and device_code grants."
        };
    }

    private static Finding AssessConfidentialClient(ConformanceReportClient client)
    {
        if (!client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode))
        {
            return new Finding
            {
                RuleId = "FC02",
                RuleName = "Confidential Client",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not use authorization_code grant."
            };
        }

        return new Finding
        {
            RuleId = "FC02",
            RuleName = "Confidential Client",
            Status = client.RequireClientSecret ? FindingStatus.Pass : FindingStatus.Fail,
            Message = client.RequireClientSecret
                ? "Client is configured as confidential (requires secret)."
                : "Client is configured as public (no secret required). FAPI 2.0 requires confidential clients for authorization_code flow.",
            Recommendation = client.RequireClientSecret ? null : "Set RequireClientSecret = true and configure appropriate client authentication."
        };
    }

    private static Finding AssessPkceS256(ConformanceReportClient client)
    {
        if (!client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode))
        {
            return new Finding
            {
                RuleId = "FC03",
                RuleName = "PKCE with S256",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not use authorization_code grant."
            };
        }

        if (!client.RequirePkce)
        {
            return new Finding
            {
                RuleId = "FC03",
                RuleName = "PKCE with S256",
                Status = FindingStatus.Fail,
                Message = "PKCE is not required. FAPI 2.0 mandates PKCE with S256.",
                Recommendation = "Set RequirePkce = true and AllowPlainTextPkce = false."
            };
        }

        if (client.AllowPlainTextPkce)
        {
            return new Finding
            {
                RuleId = "FC03",
                RuleName = "PKCE with S256",
                Status = FindingStatus.Fail,
                Message = "Plain text PKCE is allowed. FAPI 2.0 requires S256 challenge method.",
                Recommendation = "Set AllowPlainTextPkce = false."
            };
        }

        return new Finding
        {
            RuleId = "FC03",
            RuleName = "PKCE with S256",
            Status = FindingStatus.Pass,
            Message = "PKCE is required with S256 challenge method."
        };
    }

    private Finding AssessClientParRequired(ConformanceReportClient client)
    {
        if (!client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode))
        {
            return new Finding
            {
                RuleId = "FC04",
                RuleName = "PAR Required",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not use authorization_code grant."
            };
        }

        var parRequired = client.RequirePushedAuthorization || options.PushedAuthorizationRequired;

        return new Finding
        {
            RuleId = "FC04",
            RuleName = "PAR Required",
            Status = parRequired ? FindingStatus.Pass : FindingStatus.Fail,
            Message = parRequired
                ? "PAR is required for this client."
                : "PAR is not required. FAPI 2.0 mandates PAR for all authorization requests.",
            Recommendation = parRequired ? null : "Set RequirePushedAuthorization = true on the client."
        };
    }

    private static Finding AssessSenderConstrainedTokens(ConformanceReportClient client)
    {
        var hasMtlsSecrets = client.ClientSecretTypes.Any(s =>
            s == ConformanceReportSecretTypes.X509CertificateThumbprint ||
            s == ConformanceReportSecretTypes.X509CertificateName ||
            s == ConformanceReportSecretTypes.X509CertificateBase64);
        var requiresDPoP = client.RequireDPoP;

        if (hasMtlsSecrets || requiresDPoP)
        {
            return new Finding
            {
                RuleId = "FC05",
                RuleName = "Sender-Constrained Tokens",
                Status = FindingStatus.Pass,
                Message = $"Client uses sender-constrained tokens via {(requiresDPoP ? "DPoP" : "")}{(requiresDPoP && hasMtlsSecrets ? " and " : "")}{(hasMtlsSecrets ? "mTLS" : "")}."
            };
        }

        return new Finding
        {
            RuleId = "FC05",
            RuleName = "Sender-Constrained Tokens",
            Status = FindingStatus.Fail,
            Message = "Client does not use sender-constrained tokens. FAPI 2.0 requires mTLS or DPoP.",
            Recommendation = "Set RequireDPoP = true or configure mTLS certificate authentication."
        };
    }

    private static Finding AssessClientAuthentication(ConformanceReportClient client)
    {
        if (!client.RequireClientSecret)
        {
            return new Finding
            {
                RuleId = "FC06",
                RuleName = "Secure Client Authentication",
                Status = FindingStatus.Fail,
                Message = "Client is public. FAPI 2.0 requires confidential clients with private_key_jwt or mTLS authentication.",
                Recommendation = "Configure RequireClientSecret = true with private_key_jwt or mTLS."
            };
        }

        var hasPrivateKeyJwt = client.ClientSecretTypes.Any(s =>
            s == ConformanceReportSecretTypes.JsonWebKey ||
            s == ConformanceReportSecretTypes.X509CertificateBase64);

        var hasMtls = client.ClientSecretTypes.Any(s =>
            s == ConformanceReportSecretTypes.X509CertificateThumbprint ||
            s == ConformanceReportSecretTypes.X509CertificateName);

        if (hasPrivateKeyJwt || hasMtls)
        {
            var methods = new List<string>();
            if (hasPrivateKeyJwt)
            {
                methods.Add("private_key_jwt");
            }

            if (hasMtls)
            {
                methods.Add("mTLS");
            }

            return new Finding
            {
                RuleId = "FC06",
                RuleName = "Secure Client Authentication",
                Status = FindingStatus.Pass,
                Message = $"Client uses FAPI 2.0 compliant authentication: {string.Join(", ", methods)}."
            };
        }

        return new Finding
        {
            RuleId = "FC06",
            RuleName = "Secure Client Authentication",
            Status = FindingStatus.Fail,
            Message = "Client uses shared secret authentication. FAPI 2.0 requires private_key_jwt or mTLS.",
            Recommendation = "Configure client authentication using private_key_jwt (JsonWebKey secret) or mTLS (X509Certificate secret)."
        };
    }

    private static Finding AssessAuthCodeLifetime(ConformanceReportClient client)
    {
        if (!client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode))
        {
            return new Finding
            {
                RuleId = "FC07",
                RuleName = "Authorization Code Lifetime",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not use authorization_code grant."
            };
        }

        if (client.AuthorizationCodeLifetime <= MaxAuthCodeLifetimeSeconds)
        {
            return new Finding
            {
                RuleId = "FC07",
                RuleName = "Authorization Code Lifetime",
                Status = FindingStatus.Pass,
                Message = $"Authorization code lifetime is {client.AuthorizationCodeLifetime} seconds, within the FAPI 2.0 maximum of {MaxAuthCodeLifetimeSeconds} seconds."
            };
        }

        return new Finding
        {
            RuleId = "FC07",
            RuleName = "Authorization Code Lifetime",
            Status = FindingStatus.Fail,
            Message = $"Authorization code lifetime is {client.AuthorizationCodeLifetime} seconds. FAPI 2.0 requires {MaxAuthCodeLifetimeSeconds} seconds or less.",
            Recommendation = $"Set AuthorizationCodeLifetime = {MaxAuthCodeLifetimeSeconds}."
        };
    }

    private static Finding AssessRefreshTokenRotation(ConformanceReportClient client)
    {
        if (!client.AllowOfflineAccess)
        {
            return new Finding
            {
                RuleId = "FC08",
                RuleName = "Refresh Token Rotation",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not support refresh tokens (AllowOfflineAccess = false)."
            };
        }

        return new Finding
        {
            RuleId = "FC08",
            RuleName = "Refresh Token Rotation",
            Status = client.RefreshTokenUsage == ConformanceReportTokenUsage.OneTimeOnly ? FindingStatus.Pass : FindingStatus.Fail,
            Message = client.RefreshTokenUsage == ConformanceReportTokenUsage.OneTimeOnly
                ? "Refresh token rotation is enabled (one-time use)."
                : "Refresh tokens are reusable. FAPI 2.0 requires refresh token rotation.",
            Recommendation = client.RefreshTokenUsage == ConformanceReportTokenUsage.OneTimeOnly
                ? null
                : "Set RefreshTokenUsage = TokenUsage.OneTimeOnly."
        };
    }

    private static Finding AssessDPoPNonce(ConformanceReportClient client)
    {
        if (!client.RequireDPoP)
        {
            return new Finding
            {
                RuleId = "FC09",
                RuleName = "DPoP Nonce",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not require DPoP."
            };
        }

        var nonceEnabled = client.DPoPValidationMode.HasFlag(ConformanceReportDPoPValidationMode.Nonce);

        return new Finding
        {
            RuleId = "FC09",
            RuleName = "DPoP Nonce",
            Status = nonceEnabled ? FindingStatus.Pass : FindingStatus.Fail,
            Message = nonceEnabled
                ? "DPoP nonce validation is enabled."
                : "DPoP nonce validation is not enabled. FAPI 2.0 requires nonce for DPoP replay protection.",
            Recommendation = nonceEnabled ? null : "Set DPoPValidationMode to include DPoPTokenExpirationValidationMode.Nonce."
        };
    }

    private static Finding AssessExplicitRedirectUris(ConformanceReportClient client)
    {
        if (!client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode))
        {
            return new Finding
            {
                RuleId = "FC10",
                RuleName = "Explicit Redirect URIs",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not use authorization_code grant."
            };
        }

        if (client.RedirectUris.Count == 0)
        {
            return new Finding
            {
                RuleId = "FC10",
                RuleName = "Explicit Redirect URIs",
                Status = FindingStatus.Fail,
                Message = "No redirect URIs configured.",
                Recommendation = "Configure at least one explicit redirect URI."
            };
        }

        var wildcardUris = client.RedirectUris.Where(u => u.Contains('*', StringComparison.Ordinal)).ToList();

        if (wildcardUris.Count > 0)
        {
            return new Finding
            {
                RuleId = "FC10",
                RuleName = "Explicit Redirect URIs",
                Status = FindingStatus.Fail,
                Message = $"Wildcard redirect URIs detected: {string.Join(", ", wildcardUris)}. FAPI 2.0 requires exact URI matching.",
                Recommendation = "Replace wildcards with explicit URIs."
            };
        }

        return new Finding
        {
            RuleId = "FC10",
            RuleName = "Explicit Redirect URIs",
            Status = FindingStatus.Pass,
            Message = $"All {client.RedirectUris.Count} redirect URI(s) are explicit."
        };
    }

    private static Finding AssessNoAccessTokensViaBrowser(ConformanceReportClient client)
    {
        if (!client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode))
        {
            return new Finding
            {
                RuleId = "FC11",
                RuleName = "No Access Tokens via Browser",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not use authorization_code grant."
            };
        }

        return new Finding
        {
            RuleId = "FC11",
            RuleName = "No Access Tokens via Browser",
            Status = client.AllowAccessTokensViaBrowser ? FindingStatus.Fail : FindingStatus.Pass,
            Message = client.AllowAccessTokensViaBrowser
                ? "Access tokens can be transmitted via the browser. FAPI 2.0 prohibits this."
                : "Access tokens are not allowed via browser.",
            Recommendation = client.AllowAccessTokensViaBrowser ? "Set AllowAccessTokensViaBrowser = false." : null
        };
    }

    private Finding AssessRequestObject(ConformanceReportClient client)
    {
        if (!client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode))
        {
            return new Finding
            {
                RuleId = "FC12",
                RuleName = "Request Object",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not use authorization_code grant."
            };
        }

        // Request object is recommended but PAR can substitute for it
        var parRequired = client.RequirePushedAuthorization || options.PushedAuthorizationRequired;

        if (client.RequireRequestObject)
        {
            return new Finding
            {
                RuleId = "FC12",
                RuleName = "Request Object",
                Status = FindingStatus.Pass,
                Message = "Request object is required for this client."
            };
        }

        if (parRequired)
        {
            return new Finding
            {
                RuleId = "FC12",
                RuleName = "Request Object",
                Status = FindingStatus.Pass,
                Message = "PAR is required, which provides equivalent security to request objects."
            };
        }

        return new Finding
        {
            RuleId = "FC12",
            RuleName = "Request Object",
            Status = FindingStatus.Warning,
            Message = "Neither request object nor PAR is required. FAPI 2.0 recommends one of these for enhanced security.",
            Recommendation = "Set RequireRequestObject = true or RequirePushedAuthorization = true."
        };
    }
}
