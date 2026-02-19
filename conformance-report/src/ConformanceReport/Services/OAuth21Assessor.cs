// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.ConformanceReport.Models;

namespace Duende.ConformanceReport.Services;

/// <summary>
/// Assesses configuration against the OAuth 2.1 specification.
/// </summary>
internal class OAuth21Assessor(ConformanceReportServerOptions options)
{
    // Algorithms considered insecure for OAuth 2.1
    // OAuth 2.1 prohibits symmetric algorithms and deprecated algorithms
    // Note: RS256 is acceptable in OAuth 2.1, but FAPI 2.0 requires PS256/ES256
    private static readonly HashSet<string> InsecureAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        SigningAlgorithms.HmacSha256,
        SigningAlgorithms.HmacSha384,
        SigningAlgorithms.HmacSha512,
        "HS256",
        "HS384",
        "HS512",
        "none"
    };

    // Maximum recommended authorization code lifetime in seconds
    private const int MaxRecommendedAuthCodeLifetime = 60;

    // Maximum reasonable clock skew in minutes
    private const int MaxReasonableClockSkewMinutes = 5;

    /// <summary>
    /// Assesses server-level configuration against OAuth 2.1 requirements.
    /// </summary>
    public IReadOnlyList<Finding> AssessServer()
    {
        var findings = new List<Finding>();

        // S01: PKCE should be enabled (checked at client level, but we note server supports it)
        findings.Add(new Finding
        {
            RuleId = "S01",
            RuleName = "PKCE Support",
            Status = FindingStatus.Pass,
            Message = "Server supports PKCE. Individual client configurations are assessed separately."
        });

        // S02: Password grant should not be allowed (checked at client level)
        findings.Add(new Finding
        {
            RuleId = "S02",
            RuleName = "Resource Owner Password Grant Prohibition",
            Status = FindingStatus.Pass,
            Message = "Password grant usage is assessed at the client level."
        });

        // S03: PAR should be available
        findings.Add(AssessParAvailability());

        // S04: Sender-constrained tokens recommendation
        findings.Add(AssessSenderConstrainedTokenSupport());

        // S05: Signing algorithms must be secure
        findings.Add(AssessSigningAlgorithms());

        // S06: Clock skew should be reasonable
        findings.Add(AssessClockSkew());

        // S07: DPoP nonce recommendation
        findings.Add(AssessDPoPNonceConfiguration());

        // S08: HTTP 303 redirects
        findings.Add(AssessHttp303Redirects());

        return findings;
    }

    /// <summary>
    /// Assesses a client's configuration against OAuth 2.1 requirements.
    /// </summary>
    public IReadOnlyList<Finding> AssessClient(ConformanceReportClient client)
    {
        var findings = new List<Finding>();

        // C01: Only authorization_code or client_credentials grants
        findings.Add(AssessAllowedGrantTypes(client));

        // C02: PKCE required
        findings.Add(AssessPkceRequired(client));

        // C03: No plain text PKCE
        findings.Add(AssessNonPlainPkce(client));

        // C04: Explicit redirect URIs
        findings.Add(AssessExplicitRedirectUris(client));

        // C05: Confidential clients should require client secret
        findings.Add(AssessConfidentialClientSecret(client));

        // C06: PAR required recommended
        findings.Add(AssessParRequired(client));

        // C07: Sender-constrained tokens recommended
        findings.Add(AssessSenderConstrainedTokens(client));

        // C08: Auth code lifetime should be short
        findings.Add(AssessAuthCodeLifetime(client));

        // C09: Refresh token rotation recommended
        findings.Add(AssessRefreshTokenRotation(client));

        // C10: DPoP nonce if DPoP required
        findings.Add(AssessDPoPNonce(client));

        // C11: Private key JWT or mTLS for confidential clients
        findings.Add(AssessClientAuthentication(client));

        // C12: Refresh tokens recommended for authorization_code clients
        findings.Add(AssessRefreshTokenSupport(client));

        return findings;
    }

    #region Server-Level Assessments

    private Finding AssessParAvailability()
    {
        var parEnabled = options.PushedAuthorizationEndpointEnabled;

        return new Finding
        {
            RuleId = "S03",
            RuleName = "Pushed Authorization Requests (PAR)",
            Status = parEnabled ? FindingStatus.Pass : FindingStatus.Warning,
            Message = parEnabled
                ? "PAR endpoint is enabled."
                : "PAR endpoint is not enabled. PAR is recommended by OAuth 2.1 for enhanced security.",
            Recommendation = parEnabled ? null : "Enable the PAR endpoint in EndpointsOptions."
        };
    }

    private Finding AssessSenderConstrainedTokenSupport()
    {
        var mtlsEnabled = options.MutualTlsEnabled;
        var dpopSupported = true; // DPoP is always supported when enabled per-client

        if (mtlsEnabled || dpopSupported)
        {
            return new Finding
            {
                RuleId = "S04",
                RuleName = "Sender-Constrained Token Support",
                Status = FindingStatus.Pass,
                Message = $"Sender-constrained token mechanisms available: {(mtlsEnabled ? "mTLS" : "")}{(mtlsEnabled && dpopSupported ? ", " : "")}{(dpopSupported ? "DPoP" : "")}".TrimEnd(',', ' ')
            };
        }

        return new Finding
        {
            RuleId = "S04",
            RuleName = "Sender-Constrained Token Support",
            Status = FindingStatus.Warning,
            Message = "No sender-constrained token mechanisms (mTLS or DPoP) are configured at the server level.",
            Recommendation = "Consider enabling mTLS or configuring DPoP for clients to support sender-constrained tokens."
        };
    }

    private Finding AssessSigningAlgorithms()
    {
        var algorithms = options.SupportedSigningAlgorithms;
        var insecureFound = algorithms.Where(a => InsecureAlgorithms.Contains(a)).ToList();

        if (insecureFound.Count == 0)
        {
            return new Finding
            {
                RuleId = "S05",
                RuleName = "Secure Signing Algorithms",
                Status = FindingStatus.Pass,
                Message = "All configured signing algorithms are considered secure."
            };
        }

        return new Finding
        {
            RuleId = "S05",
            RuleName = "Secure Signing Algorithms",
            Status = FindingStatus.Fail,
            Message = $"Insecure signing algorithms are configured: {string.Join(", ", insecureFound)}. OAuth 2.1 requires asymmetric algorithms.",
            Recommendation = "Remove symmetric (HS*) and deprecated algorithms. Use RS256, RS384, RS512, PS256, PS384, PS512, ES256, ES384, or ES512."
        };
    }

    private Finding AssessClockSkew()
    {
        var clockSkew = options.JwtValidationClockSkew;

        if (clockSkew <= TimeSpan.FromMinutes(MaxReasonableClockSkewMinutes))
        {
            return new Finding
            {
                RuleId = "S06",
                RuleName = "JWT Clock Skew",
                Status = FindingStatus.Pass,
                Message = $"JWT clock skew is set to {clockSkew.TotalMinutes} minutes, which is within the recommended range."
            };
        }

        return new Finding
        {
            RuleId = "S06",
            RuleName = "JWT Clock Skew",
            Status = FindingStatus.Warning,
            Message = $"JWT clock skew is set to {clockSkew.TotalMinutes} minutes, which exceeds the recommended maximum of {MaxReasonableClockSkewMinutes} minutes.",
            Recommendation = $"Consider reducing JwtValidationClockSkew to {MaxReasonableClockSkewMinutes} minutes or less."
        };
    }

    // DPoP nonce configuration is set per-client via DPoPValidationMode
    // At server level, we just note that DPoP is supported and nonce can be configured per-client
    private static Finding AssessDPoPNonceConfiguration() =>
        new()
        {
            RuleId = "S07",
            RuleName = "DPoP Nonce Support",
            Status = FindingStatus.Pass,
            Message = "DPoP is supported. Nonce validation can be configured per-client via DPoPValidationMode. Individual client configurations are assessed separately."
        };

    private Finding AssessHttp303Redirects() =>
        new()
        {
            RuleId = "S08",
            RuleName = "HTTP 303 Redirects",
            Status = options.UseHttp303Redirects ? FindingStatus.Pass : FindingStatus.Warning,
            Message = options.UseHttp303Redirects
                ? "HTTP 303 (See Other) redirects are enabled."
                : "HTTP 302 (Found) redirects are used. HTTP 303 is recommended to prevent POST data resubmission.",
            Recommendation = options.UseHttp303Redirects ? null : "Set UseHttp303Redirects = true in IdentityServerOptions."
        };

    #endregion

    #region Client-Level Assessments

    private static Finding AssessAllowedGrantTypes(ConformanceReportClient client)
    {
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
                RuleId = "C01",
                RuleName = "OAuth 2.1 Grant Types",
                Status = FindingStatus.Pass,
                Message = $"Client uses only OAuth 2.1 compliant grant types: {string.Join(", ", client.AllowedGrantTypes)}."
            };
        }

        return new Finding
        {
            RuleId = "C01",
            RuleName = "OAuth 2.1 Grant Types",
            Status = FindingStatus.Fail,
            Message = $"Client uses non-OAuth 2.1 grant types: {string.Join(", ", disallowedGrants)}. OAuth 2.1 only allows authorization_code, client_credentials, and refresh_token.",
            Recommendation = "Remove implicit and password grants. Use authorization_code with PKCE for user authentication."
        };
    }

    private static Finding AssessPkceRequired(ConformanceReportClient client)
    {
        // PKCE is only applicable to authorization_code grant
        if (!client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode))
        {
            return new Finding
            {
                RuleId = "C02",
                RuleName = "PKCE Required",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not use authorization_code grant, so PKCE is not applicable."
            };
        }

        return new Finding
        {
            RuleId = "C02",
            RuleName = "PKCE Required",
            Status = client.RequirePkce ? FindingStatus.Pass : FindingStatus.Fail,
            Message = client.RequirePkce
                ? "PKCE is required for this client."
                : "PKCE is not required. OAuth 2.1 mandates PKCE for all authorization_code clients.",
            Recommendation = client.RequirePkce ? null : "Set RequirePkce = true on the client."
        };
    }

    private static Finding AssessNonPlainPkce(ConformanceReportClient client)
    {
        if (!client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode))
        {
            return new Finding
            {
                RuleId = "C03",
                RuleName = "No Plain Text PKCE",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not use authorization_code grant, so PKCE settings are not applicable."
            };
        }

        return new Finding
        {
            RuleId = "C03",
            RuleName = "No Plain Text PKCE",
            Status = client.AllowPlainTextPkce ? FindingStatus.Fail : FindingStatus.Pass,
            Message = client.AllowPlainTextPkce
                ? "Plain text PKCE is allowed. OAuth 2.1 requires S256 challenge method."
                : "Plain text PKCE is not allowed; S256 challenge method is enforced.",
            Recommendation = client.AllowPlainTextPkce ? "Set AllowPlainTextPkce = false on the client." : null
        };
    }

    private static Finding AssessExplicitRedirectUris(ConformanceReportClient client)
    {
        if (!client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode))
        {
            return new Finding
            {
                RuleId = "C04",
                RuleName = "Explicit Redirect URIs",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not use authorization_code grant, so redirect URI validation is not applicable."
            };
        }

        if (client.RedirectUris.Count == 0)
        {
            return new Finding
            {
                RuleId = "C04",
                RuleName = "Explicit Redirect URIs",
                Status = FindingStatus.Fail,
                Message = "No redirect URIs are configured. At least one explicit redirect URI is required.",
                Recommendation = "Configure at least one explicit redirect URI for the client."
            };
        }

        var wildcardUris = client.RedirectUris
            .Where(u => u.Contains('*', StringComparison.Ordinal))
            .ToList();

        if (wildcardUris.Count > 0)
        {
            return new Finding
            {
                RuleId = "C04",
                RuleName = "Explicit Redirect URIs",
                Status = FindingStatus.Fail,
                Message = $"Wildcard redirect URIs detected: {string.Join(", ", wildcardUris)}. OAuth 2.1 requires exact URI matching.",
                Recommendation = "Replace wildcard redirect URIs with explicit, fully-qualified URIs."
            };
        }

        return new Finding
        {
            RuleId = "C04",
            RuleName = "Explicit Redirect URIs",
            Status = FindingStatus.Pass,
            Message = $"All {client.RedirectUris.Count} redirect URI(s) are explicit with no wildcards."
        };
    }

    private static Finding AssessConfidentialClientSecret(ConformanceReportClient client)
    {
        // Public clients (authorization_code without secret) are allowed in OAuth 2.1
        // but they must use PKCE
        if (!client.RequireClientSecret)
        {
            // This is a public client - check if it's using authorization_code with PKCE
            if (client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode) && client.RequirePkce)
            {
                return new Finding
                {
                    RuleId = "C05",
                    RuleName = "Client Authentication",
                    Status = FindingStatus.Pass,
                    Message = "Public client using authorization_code with PKCE, which is permitted by OAuth 2.1."
                };
            }

            return new Finding
            {
                RuleId = "C05",
                RuleName = "Client Authentication",
                Status = FindingStatus.Warning,
                Message = "Client does not require a secret. Consider whether this client should be confidential.",
                Recommendation = "For confidential clients, set RequireClientSecret = true and configure client secrets."
            };
        }

        // Confidential client - should have secrets
        if (client.ClientSecretTypes.Count == 0)
        {
            return new Finding
            {
                RuleId = "C05",
                RuleName = "Client Authentication",
                Status = FindingStatus.Fail,
                Message = "Confidential client (RequireClientSecret = true) has no secrets configured.",
                Recommendation = "Add client secrets or use private_key_jwt/mTLS for authentication."
            };
        }

        return new Finding
        {
            RuleId = "C05",
            RuleName = "Client Authentication",
            Status = FindingStatus.Pass,
            Message = "Confidential client has secrets configured."
        };
    }

    private Finding AssessParRequired(ConformanceReportClient client)
    {
        if (!client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode))
        {
            return new Finding
            {
                RuleId = "C06",
                RuleName = "PAR Required",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not use authorization_code grant, so PAR is not applicable."
            };
        }

        var parRequired = client.RequirePushedAuthorization || options.PushedAuthorizationRequired;

        return new Finding
        {
            RuleId = "C06",
            RuleName = "PAR Required",
            Status = parRequired ? FindingStatus.Pass : FindingStatus.Warning,
            Message = parRequired
                ? "PAR is required for this client."
                : "PAR is not required. Consider requiring PAR for enhanced security.",
            Recommendation = parRequired ? null : "Set RequirePushedAuthorization = true on the client."
        };
    }

    private static Finding AssessSenderConstrainedTokens(ConformanceReportClient client)
    {
        // Check for mTLS or DPoP
        var hasMtlsSecrets = client.ClientSecretTypes.Any(s =>
            s == ConformanceReportSecretTypes.X509CertificateThumbprint ||
            s == ConformanceReportSecretTypes.X509CertificateName ||
            s == ConformanceReportSecretTypes.X509CertificateBase64);
        var requiresDPoP = client.RequireDPoP;

        if (hasMtlsSecrets || requiresDPoP)
        {
            return new Finding
            {
                RuleId = "C07",
                RuleName = "Sender-Constrained Tokens",
                Status = FindingStatus.Pass,
                Message = $"Client uses sender-constrained tokens via {(requiresDPoP ? "DPoP" : "")}{(requiresDPoP && hasMtlsSecrets ? " and " : "")}{(hasMtlsSecrets ? "mTLS" : "")}."
            };
        }

        return new Finding
        {
            RuleId = "C07",
            RuleName = "Sender-Constrained Tokens",
            Status = FindingStatus.Warning,
            Message = "Client does not use sender-constrained tokens (mTLS or DPoP).",
            Recommendation = "Consider requiring DPoP (RequireDPoP = true) or configuring mTLS certificate authentication."
        };
    }

    private static Finding AssessAuthCodeLifetime(ConformanceReportClient client)
    {
        if (!client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode))
        {
            return new Finding
            {
                RuleId = "C08",
                RuleName = "Authorization Code Lifetime",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not use authorization_code grant."
            };
        }

        if (client.AuthorizationCodeLifetime <= MaxRecommendedAuthCodeLifetime)
        {
            return new Finding
            {
                RuleId = "C08",
                RuleName = "Authorization Code Lifetime",
                Status = FindingStatus.Pass,
                Message = $"Authorization code lifetime is {client.AuthorizationCodeLifetime} seconds, which is within the recommended maximum of {MaxRecommendedAuthCodeLifetime} seconds."
            };
        }

        return new Finding
        {
            RuleId = "C08",
            RuleName = "Authorization Code Lifetime",
            Status = FindingStatus.Warning,
            Message = $"Authorization code lifetime is {client.AuthorizationCodeLifetime} seconds. OAuth 2.1 recommends a short lifetime (e.g., {MaxRecommendedAuthCodeLifetime} seconds).",
            Recommendation = $"Consider reducing AuthorizationCodeLifetime to {MaxRecommendedAuthCodeLifetime} seconds or less."
        };
    }

    private static Finding AssessRefreshTokenRotation(ConformanceReportClient client)
    {
        if (!client.AllowOfflineAccess)
        {
            return new Finding
            {
                RuleId = "C09",
                RuleName = "Refresh Token Rotation",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not support refresh tokens (AllowOfflineAccess = false)."
            };
        }

        return new Finding
        {
            RuleId = "C09",
            RuleName = "Refresh Token Rotation",
            Status = client.RefreshTokenUsage == ConformanceReportTokenUsage.OneTimeOnly ? FindingStatus.Pass : FindingStatus.Warning,
            Message = client.RefreshTokenUsage == ConformanceReportTokenUsage.OneTimeOnly
                ? "Refresh token rotation is enabled (one-time use)."
                : "Refresh tokens are reusable. OAuth 2.1 recommends one-time use refresh tokens.",
            Recommendation = client.RefreshTokenUsage == ConformanceReportTokenUsage.OneTimeOnly
                ? null
                : "Set RefreshTokenUsage = TokenUsage.OneTimeOnly for refresh token rotation."
        };
    }

    private static Finding AssessDPoPNonce(ConformanceReportClient client)
    {
        if (!client.RequireDPoP)
        {
            return new Finding
            {
                RuleId = "C10",
                RuleName = "DPoP Nonce Validation",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not require DPoP."
            };
        }

        var nonceEnabled = client.DPoPValidationMode.HasFlag(ConformanceReportDPoPValidationMode.Nonce);

        return new Finding
        {
            RuleId = "C10",
            RuleName = "DPoP Nonce Validation",
            Status = nonceEnabled ? FindingStatus.Pass : FindingStatus.Warning,
            Message = nonceEnabled
                ? "DPoP nonce validation is enabled for this client."
                : "DPoP nonce validation is not enabled. This is recommended for enhanced replay protection.",
            Recommendation = nonceEnabled ? null : "Set DPoPValidationMode to include DPoPTokenExpirationValidationMode.Nonce."
        };
    }

    private static Finding AssessClientAuthentication(ConformanceReportClient client)
    {
        if (!client.RequireClientSecret)
        {
            return new Finding
            {
                RuleId = "C11",
                RuleName = "Secure Client Authentication",
                Status = FindingStatus.NotApplicable,
                Message = "Client is a public client (does not require a secret)."
            };
        }

        // Check for private_key_jwt (JWT Bearer) or mTLS authentication
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
                RuleId = "C11",
                RuleName = "Secure Client Authentication",
                Status = FindingStatus.Pass,
                Message = $"Client uses secure authentication method(s): {string.Join(", ", methods)}."
            };
        }

        // Client uses shared secret
        return new Finding
        {
            RuleId = "C11",
            RuleName = "Secure Client Authentication",
            Status = FindingStatus.Warning,
            Message = "Client uses shared secret authentication. OAuth 2.1 recommends private_key_jwt or mTLS for confidential clients.",
            Recommendation = "Consider migrating to private_key_jwt or mTLS authentication for enhanced security."
        };
    }

    private static Finding AssessRefreshTokenSupport(ConformanceReportClient client)
    {
        if (!client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode))
        {
            return new Finding
            {
                RuleId = "C12",
                RuleName = "Refresh Token Support",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not use authorization_code grant."
            };
        }

        return new Finding
        {
            RuleId = "C12",
            RuleName = "Refresh Token Support",
            Status = client.AllowOfflineAccess ? FindingStatus.Pass : FindingStatus.Warning,
            Message = client.AllowOfflineAccess
                ? "Refresh tokens are enabled for this client."
                : "Refresh tokens are not enabled. Consider enabling for better user experience without compromising security.",
            Recommendation = client.AllowOfflineAccess ? null : "Set AllowOfflineAccess = true to enable refresh tokens."
        };
    }

    #endregion
}
