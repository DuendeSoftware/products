// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.ConformanceReport.Internal.Models;

namespace Duende.ConformanceReport.Services;

/// <summary>
/// Assesses configuration against the OAuth 2.1 specification.
/// </summary>
internal class OAuth21Assessor(ConformanceReportServerOptions options)
{
    // Maximum recommended authorization code lifetime in seconds.
    // OAuth 2.1 section 4.1.2: "A maximum authorization code lifetime of 10 minutes is RECOMMENDED."
    private const int MaxRecommendedAuthCodeLifetime = 600;

    /// <summary>
    /// Assesses server-level configuration against OAuth 2.1 requirements.
    /// </summary>
    public IReadOnlyList<Finding> AssessServer(IReadOnlyCollection<ConformanceReportClient> clients) =>
        [
            // S01: PKCE support (fixed server capability; per-client enforcement is C02/C03).
            AssessPkceSupport(),
            // S02: no client uses a grant type removed by OAuth 2.1 (same determination as C01).
            AssessRemovedGrantTypes(clients),
            // S04: sender-constrained token support (fixed server capability; per-client is C07).
            AssessSenderConstrainedTokenSupport(),
            // S08: HTTP 303 redirects (fixed IdentityServer behavior).
            AssessHttp303Redirects(),
            // S09: issuer identification response parameter (new rule).
            AssessIssuerIdentification()

            // S03, S05, S06 and S07 were retired because OAuth 2.1 doesn't require what they
            // checked. Their IDs are not reused.
        ];

    /// <summary>
    /// Assesses a client's configuration against OAuth 2.1 requirements.
    /// </summary>
    public IReadOnlyList<Finding> AssessClient(ConformanceReportClient client) =>
        [
            AssessAllowedGrantTypes(client),
            AssessPkceRequired(client),
            AssessNonPlainPkce(client),
            AssessExplicitRedirectUris(client),
            AssessConfidentialClientSecret(client),
            AssessSenderConstrainedTokens(client),
            AssessAuthCodeLifetime(client),
            AssessRefreshTokenRotation(client),
            AssessClientAuthentication(client)
        ];

    // OAuth 2.1 §4.1.1: the authorization server MUST support code_challenge / code_verifier
    // (PKCE) for all clients. This records the fixed IdentityServer capability; whether a
    // specific client is configured to require PKCE is assessed by C02/C03.
    private static Finding AssessPkceSupport() =>
        new()
        {
            RuleId = "S01",
            RuleName = "PKCE Support",
            Status = FindingStatus.Pass,
            Message = "IdentityServer supports code_challenge and code_verifier (PKCE) for all clients. OAuth 2.1 (§4.1.1) requires the authorization server to support PKCE. Per-client enforcement is assessed by C02/C03."
        };

    // OAuth 2.1 §10 / §10.1: summarizes, at the server level, whether any client uses a grant
    // type removed by OAuth 2.1. Uses the same disallowed-grant determination as C01.
    private static Finding AssessRemovedGrantTypes(IReadOnlyCollection<ConformanceReportClient> clients)
    {
        var offendingClients = clients
            .Select(client => (client.ClientId, DisallowedGrants: GetDisallowedGrantTypes(client)))
            .Where(c => c.DisallowedGrants.Count > 0)
            .ToList();

        if (offendingClients.Count == 0)
        {
            return new Finding
            {
                RuleId = "S02",
                RuleName = "Removed Grant Types",
                Status = FindingStatus.Pass,
                Message = "No client uses a grant type removed by OAuth 2.1."
            };
        }

        var details = string.Join("; ", offendingClients.Select(c => $"{c.ClientId} ({string.Join(", ", c.DisallowedGrants)})"));

        return new Finding
        {
            RuleId = "S02",
            RuleName = "Removed Grant Types",
            Status = FindingStatus.Fail,
            Message = $"The following clients use grant types removed by OAuth 2.1: {details}. OAuth 2.1 (§10, §10.1) removes the password grant and the issuing of access tokens from the authorization endpoint (implicit or hybrid with AllowAccessTokensViaBrowser).",
            Recommendation = "Remove the disallowed grant(s) from the affected clients. Use authorization_code with PKCE for user authentication."
        };
    }

    // OAuth 2.1 §1.4.3: sender-constrained tokens (DPoP or mTLS) are recommended. This
    // records whether the server is capable of issuing sender-constrained tokens at all;
    // whether a specific client uses one is assessed by C07.
    private Finding AssessSenderConstrainedTokenSupport()
    {
        var dpopSupported = options.DPoPSigningAlgorithms.Count > 0;
        var mtlsSupported = options.MutualTlsEnabled;

        if (dpopSupported || mtlsSupported)
        {
            var mechanisms = new List<string>();
            if (dpopSupported)
            {
                mechanisms.Add("DPoP");
            }

            if (mtlsSupported)
            {
                mechanisms.Add("mTLS");
            }

            return new Finding
            {
                RuleId = "S04",
                RuleName = "Sender-Constrained Token Support",
                Status = FindingStatus.Pass,
                Message = $"The server supports sender-constrained tokens via: {string.Join(", ", mechanisms)}."
            };
        }

        return new Finding
        {
            RuleId = "S04",
            RuleName = "Sender-Constrained Token Support",
            Status = FindingStatus.Warning,
            Message = "The server does not support DPoP or mTLS sender-constrained tokens. OAuth 2.1 (§1.4.3) recommends sender-constrained tokens.",
            Recommendation = "Configure IdentityServerOptions.DPoP.SupportedDPoPSigningAlgorithms, or enable mTLS."
        };
    }

    // OAuth 2.1 §7.5.4: the authorization server MUST NOT use HTTP 307 for redirects and
    // SHOULD use HTTP 303. IdentityServer always uses HTTP 303 (See Other) for redirects.
    private static Finding AssessHttp303Redirects() =>
        new()
        {
            RuleId = "S08",
            RuleName = "HTTP 303 Redirects",
            Status = FindingStatus.Pass,
            Message = "IdentityServer always uses HTTP 303 (See Other) for redirects. OAuth 2.1 (§7.5.4) prohibits 307 for redirects that may carry credentials and recommends 303."
        };

    // OAuth 2.1 section 4.1.2: "iss" is REQUIRED in the authorization response.
    private Finding AssessIssuerIdentification() =>
        new()
        {
            RuleId = "S09",
            RuleName = "Issuer Identification",
            Status = options.EmitIssuerIdentificationResponseParameter ? FindingStatus.Pass : FindingStatus.Fail,
            Message = options.EmitIssuerIdentificationResponseParameter
                ? "Issuer identification response parameter (iss) is enabled."
                : "Issuer identification response parameter is not enabled. OAuth 2.1 (§4.1.2) requires the iss parameter in the authorization response.",
            Recommendation = options.EmitIssuerIdentificationResponseParameter
                ? null
                : "Set EmitIssuerIdentificationResponseParameter = true."
        };

    // OAuth 2.1 section 10 omits the implicit and password grants from the base
    // spec; implicit and hybrid flows are disallowed when access tokens can
    // leak through the browser (AllowAccessTokensViaBrowser). All other grants,
    // including extension grants such as device_code, token exchange, and CIBA,
    // are allowed (§1.3, §4.4).
    private static Finding AssessAllowedGrantTypes(ConformanceReportClient client)
    {
        var disallowedGrants = GetDisallowedGrantTypes(client);

        if (disallowedGrants.Count == 0)
        {
            return new Finding
            {
                RuleId = "C01",
                RuleName = "OAuth 2.1 Grant Types",
                Status = FindingStatus.Pass,
                Message = $"Client does not use grant types prohibited by OAuth 2.1: {string.Join(", ", client.AllowedGrantTypes)}."
            };
        }

        return new Finding
        {
            RuleId = "C01",
            RuleName = "OAuth 2.1 Grant Types",
            Status = FindingStatus.Fail,
            Message = $"Client uses grant types prohibited by OAuth 2.1: {string.Join(", ", disallowedGrants)}. OAuth 2.1 (§10, §10.1) removes the password grant and the issuing of access tokens from the authorization endpoint (implicit or hybrid with AllowAccessTokensViaBrowser).",
            Recommendation = "Remove the disallowed grant(s). Use authorization_code with PKCE for user authentication."
        };
    }

    // Shared by C01 (per-client) and S02 (server-level summary): determines which grants on a
    // client are removed by OAuth 2.1. See §10, §10.1.
    private static List<string> GetDisallowedGrantTypes(ConformanceReportClient client)
    {
        var disallowedGrants = new List<string>();

        if (client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.Implicit) && client.AllowAccessTokensViaBrowser)
        {
            disallowedGrants.Add(ConformanceReportGrantTypes.Implicit);
        }

        if (client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.Password))
        {
            disallowedGrants.Add(ConformanceReportGrantTypes.Password);
        }

        if (client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.Hybrid) && client.AllowAccessTokensViaBrowser)
        {
            disallowedGrants.Add(ConformanceReportGrantTypes.Hybrid);
        }

        return disallowedGrants;
    }

    private static bool UsesCodeFlow(ConformanceReportClient client) =>
        client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode) ||
        client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.Hybrid);

    // OAuth 2.1 §4.1.1 / §4.1.2.1 / §7.5.1: PKCE is RECOMMENDED but the AS MUST reject requests
    // without a code_challenge from public clients and other clients unless there is reasonable
    // assurance the client mitigates authorization code injection another way. Configuration alone
    // cannot provide that assurance, so Fail applies to all authorization_code clients.
    private static Finding AssessPkceRequired(ConformanceReportClient client)
    {
        // PKCE is only applicable to authorization_code grant
        if (!UsesCodeFlow(client))
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
                : "PKCE is not required. OAuth 2.1 (§4.1.2.1) requires PKCE unless the client mitigates authorization code injection in another way, which can't be verified from configuration. Set RequirePkce = true.",
            Recommendation = client.RequirePkce ? null : "Set RequirePkce = true on the client."
        };
    }

    // OAuth 2.1 §4.1.1, §7.5.2: the plain PKCE transform is prohibited.
    private static Finding AssessNonPlainPkce(ConformanceReportClient client)
    {
        if (!UsesCodeFlow(client))
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

    // OAuth 2.1 §1.5, §2.3, §2.3.1, §8.4.1-§8.4.3: redirect URIs must be absolute with no
    // fragment, must use https except for loopback, and private-use schemes are only permitted
    // with caveats. The wildcard check is removed: IdentityServer's StrictRedirectUriValidator
    // does exact string matching, so a literal '*' character is not a real wildcard.
    private Finding AssessExplicitRedirectUris(ConformanceReportClient client)
    {
        if (!UsesCodeFlow(client))
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
            return AssessMissingRedirectUris(client);
        }

        var failMessages = new List<string>();
        var warnMessages = new List<string>();

        foreach (var redirectUri in client.RedirectUris)
        {
            CollectRedirectUriFindings(redirectUri, failMessages, warnMessages);
        }

        if (failMessages.Count > 0)
        {
            return new Finding
            {
                RuleId = "C04",
                RuleName = "Explicit Redirect URIs",
                Status = FindingStatus.Fail,
                Message = string.Join(" ", failMessages.Concat(warnMessages)),
                Recommendation = "Register only absolute https redirect URIs with no fragment. Use http only with a loopback IP address (127.0.0.1 or [::1]) for native clients, and register StrictRedirectUriValidatorAppAuth (AddAppAuthRedirectUriValidator)."
            };
        }

        if (warnMessages.Count > 0)
        {
            return new Finding
            {
                RuleId = "C04",
                RuleName = "Explicit Redirect URIs",
                Status = FindingStatus.Warning,
                Message = string.Join(" ", warnMessages)
            };
        }

        return new Finding
        {
            RuleId = "C04",
            RuleName = "Explicit Redirect URIs",
            Status = FindingStatus.Pass,
            Message = $"All {client.RedirectUris.Count} redirect URI(s) are explicit and use allowed schemes."
        };
    }

    // No registered redirect URIs is only valid when the client supplies them through an
    // authenticated PAR request (§2.3.1 allows runtime registration), which mirrors
    // DefaultClientConfigurationValidator's AllowUnregisteredPushedRedirectUris exemption. The
    // PAR endpoint must also be enabled, or the client has no way to supply a redirect URI.
    private Finding AssessMissingRedirectUris(ConformanceReportClient client)
    {
        if (options.AllowUnregisteredPushedRedirectUris && client.RequireClientSecret)
        {
            if (!options.PushedAuthorizationEndpointEnabled)
            {
                return new Finding
                {
                    RuleId = "C04",
                    RuleName = "Explicit Redirect URIs",
                    Status = FindingStatus.Fail,
                    Message = "No redirect URIs are registered, and the PAR endpoint is disabled, so the client can't supply one. AllowUnregisteredPushedRedirectUris only applies to redirect URIs sent in a PAR request.",
                    Recommendation = "Enable the PAR endpoint, or configure at least one explicit redirect URI for the client."
                };
            }

            return new Finding
            {
                RuleId = "C04",
                RuleName = "Explicit Redirect URIs",
                Status = FindingStatus.Pass,
                Message = "No redirect URIs are registered. Redirect URIs are supplied via an authenticated PAR request (§2.3.1), which is permitted when AllowUnregisteredPushedRedirectUris is enabled."
            };
        }

        return new Finding
        {
            RuleId = "C04",
            RuleName = "Explicit Redirect URIs",
            Status = FindingStatus.Fail,
            Message = "No redirect URIs are configured. At least one explicit redirect URI is required.",
            Recommendation = "Configure at least one explicit redirect URI for the client."
        };
    }

    private void CollectRedirectUriFindings(string redirectUri, List<string> failMessages, List<string> warnMessages)
    {
        if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
        {
            // System.Uri rejects "*" in a host name, so a wildcard host would otherwise be
            // reported as a relative URI.
            if (RedirectUriWildcards.HasWildcardHost(redirectUri))
            {
                failMessages.Add($"Redirect URI '{redirectUri}' has a wildcard ('*') in the host. IdentityServer compares redirect URIs by exact string match and doesn't support wildcards, so this URI can never match a real redirect. OAuth 2.1 (§2.3.1) requires each complete redirect URI to be registered.");
                return;
            }

            failMessages.Add($"Redirect URI '{redirectUri}' is not an absolute URI (§2.3).");
            return;
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            failMessages.Add($"Redirect URI '{redirectUri}' contains a fragment, which is prohibited (§2.3).");
            return;
        }

        var isHttp = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        var isHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

        if (isHttp)
        {
            if (!uri.IsLoopback)
            {
                failMessages.Add($"Redirect URI '{redirectUri}' uses the http scheme with a non-loopback host. OAuth 2.1 (§1.5) requires https except for loopback redirects.");
                return;
            }

            // Matches FAPI FC13: IdentityServer only accepts loopback redirects when the server has
            // opted in to loopback redirection (StrictRedirectUriValidatorAppAuth).
            if (!options.LoopbackRedirectUrisEnabled)
            {
                failMessages.Add($"Redirect URI '{redirectUri}' is a loopback http redirect URI, but the server is not configured for loopback redirection. OAuth 2.1 (§8.4.2) permits loopback redirects for native clients.");
                return;
            }

            // RFC 8252/OAuth 2.1 §8.4.2: "localhost" is NOT RECOMMENDED for loopback redirection.
            if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                warnMessages.Add($"Loopback redirect URI '{redirectUri}' uses \"localhost\" rather than a loopback IP literal, which OAuth 2.1 (§8.4.2) does not recommend.");
            }

            return;
        }

        if (isHttps)
        {
            return;
        }

        // Private-use scheme (§8.4.3). A reverse-domain-name scheme (containing a period) is
        // the recommended form but is still cautioned against; schemes without a period are
        // an even weaker signal and SHOULD be rejected per §2.3.1.
        if (uri.Scheme.Contains('.', StringComparison.Ordinal))
        {
            warnMessages.Add($"Redirect URI '{redirectUri}' uses a private-use URI scheme. Private-use URI schemes are permitted by OAuth 2.1 but are vulnerable to scheme hijacking on some platforms. Prefer claimed https redirect URIs where the platform supports them (§8.4.1, §8.4.3).");
        }
        else
        {
            warnMessages.Add($"Redirect URI '{redirectUri}' uses a private-use URI scheme without a reverse-domain-name format (no period). OAuth 2.1 (§2.3.1) recommends rejecting such schemes.");
        }
    }

    // OAuth 2.1 §2.1, §2.4: client authentication requirements.
    private static Finding AssessConfidentialClientSecret(ConformanceReportClient client)
    {
        // Public clients (authorization_code without secret) are allowed in OAuth 2.1
        // but they must use PKCE
        if (!client.RequireClientSecret)
        {
            if (client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.ClientCredentials))
            {
                return new Finding
                {
                    RuleId = "C05",
                    RuleName = "Client Authentication",
                    Status = FindingStatus.Fail,
                    Message = "Public clients must not use the client_credentials grant. OAuth 2.1 (§4.2) requires this grant to be used only by confidential clients.",
                    Recommendation = "Require client authentication for clients using the client_credentials grant."
                };
            }

            // This is a public client - check if it uses a code flow (authorization_code or hybrid) with PKCE
            if (UsesCodeFlow(client) && client.RequirePkce)
            {
                return new Finding
                {
                    RuleId = "C05",
                    RuleName = "Client Authentication",
                    Status = FindingStatus.Pass,
                    Message = "Public client using a code flow with PKCE, which is permitted by OAuth 2.1."
                };
            }

            if (!UsesCodeFlow(client))
            {
                return new Finding
                {
                    RuleId = "C05",
                    RuleName = "Client Authentication",
                    Status = FindingStatus.NotApplicable,
                    Message = "Client does not use a code flow (authorization_code or hybrid) or the client_credentials grant, so client authentication requirements are not applicable."
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

    // Uses the same DPoP-bound / mTLS-bound definition as FAPI 2.0 FC05 (see SenderConstraint),
    // but sender-constraining is only a SHOULD in OAuth 2.1 (§1.4.3), so the failing result is
    // a Warning rather than a Fail.
    private Finding AssessSenderConstrainedTokens(ConformanceReportClient client)
    {
        var dpopBound = SenderConstraint.IsDPoPBound(options, client);
        var mtlsBound = SenderConstraint.IsMtlsBound(options, client);

        if (mtlsBound || dpopBound)
        {
            return new Finding
            {
                RuleId = "C07",
                RuleName = "Sender-Constrained Tokens",
                Status = FindingStatus.Pass,
                Message = $"Client uses sender-constrained tokens via {(dpopBound ? "DPoP" : "")}{(dpopBound && mtlsBound ? " and " : "")}{(mtlsBound ? "mTLS" : "")}."
            };
        }

        return new Finding
        {
            RuleId = "C07",
            RuleName = "Sender-Constrained Tokens",
            Status = FindingStatus.Warning,
            Message = client.RequireDPoP
                ? "Client requires DPoP, but no DPoP proof algorithms are configured, so the server rejects every DPoP proof. The client does not use mTLS either. OAuth 2.1 (§1.4.3) recommends sender-constrained tokens (DPoP or mTLS)."
                : "Client does not use sender-constrained tokens. OAuth 2.1 (§1.4.3) recommends sender-constrained tokens (DPoP or mTLS).",
            Recommendation = client.RequireDPoP
                ? "Configure IdentityServerOptions.DPoP.SupportedDPoPSigningAlgorithms, or enable mTLS and ensure that all client secrets are the X509 thumbprint or name secrets used by mTLS."
                : "Set RequireDPoP = true (with IdentityServerOptions.DPoP.SupportedDPoPSigningAlgorithms configured), or enable mTLS and ensure that all client secrets are the X509 thumbprint or name secrets used by mTLS."
        };
    }

    // OAuth 2.1 §4.1.2: "A maximum authorization code lifetime of 10 minutes is RECOMMENDED."
    private static Finding AssessAuthCodeLifetime(ConformanceReportClient client)
    {
        if (!UsesCodeFlow(client))
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
            Message = $"Authorization code lifetime is {client.AuthorizationCodeLifetime} seconds. OAuth 2.1 (§4.1.2) recommends a maximum authorization code lifetime of 10 minutes ({MaxRecommendedAuthCodeLifetime} seconds).",
            Recommendation = $"Consider reducing AuthorizationCodeLifetime to {MaxRecommendedAuthCodeLifetime} seconds or less."
        };
    }

    // OAuth 2.1 §4.3.1: "Authorization servers MUST utilize one of these methods to detect refresh
    // token replay by malicious actors for public clients": sender-constrained refresh tokens, or
    // refresh token rotation. §4.3.1 defines rotation as retaining the relationship to invalidated
    // tokens and revoking the active token and grant on replay. IdentityServer's OneTimeOnly usage
    // deletes used tokens by default, and when they are retained it rejects the replayed token
    // without revoking the grant, so OneTimeOnly alone can't be verified as compliant from
    // configuration. Confidential clients can authenticate, so the requirement doesn't apply to them.
    private Finding AssessRefreshTokenRotation(ConformanceReportClient client)
    {
        if (!client.AllowOfflineAccess)
        {
            return RefreshTokenRotationFinding(
                FindingStatus.NotApplicable,
                "Client does not support refresh tokens (AllowOfflineAccess = false).");
        }

        if (client.RequireClientSecret)
        {
            return RefreshTokenRotationFinding(
                FindingStatus.NotApplicable,
                "Client is confidential and can authenticate, so refresh token replay detection for public clients (§4.3.1) does not apply.");
        }

        if (SenderConstraint.IsDPoPBound(options, client))
        {
            return RefreshTokenRotationFinding(
                FindingStatus.Pass,
                "Public client uses DPoP-bound (sender-constrained) refresh tokens, which OAuth 2.1 (§4.3.1) accepts for refresh token replay detection.");
        }

        if (client.RefreshTokenUsage == ConformanceReportTokenUsage.OneTimeOnly)
        {
            return RefreshTokenRotationFinding(
                FindingStatus.Warning,
                "Public client uses one-time-only refresh tokens without a sender constraint. OAuth 2.1 (§4.3.1) requires refresh token rotation to detect replay and revoke the active refresh token and grant. IdentityServer rejects a reused refresh token but does not revoke the grant without customization, so compliance can't be verified from configuration.",
                "Set RequireDPoP = true and configure IdentityServerOptions.DPoP.SupportedDPoPSigningAlgorithms, or customize DefaultRefreshTokenService (with PersistentGrantOptions.DeleteOneTimeOnlyRefreshTokensOnUse = false) to revoke the grant when a consumed refresh token is reused.");
        }

        return RefreshTokenRotationFinding(
            FindingStatus.Fail,
            "Public client uses reusable refresh tokens with no sender constraint. OAuth 2.1 (§4.3.1) requires sender-constrained refresh tokens or refresh token rotation to detect replay for public clients.",
            "Set RequireDPoP = true and configure IdentityServerOptions.DPoP.SupportedDPoPSigningAlgorithms. Setting RefreshTokenUsage = TokenUsage.OneTimeOnly prevents token reuse, but full replay detection also requires customizing DefaultRefreshTokenService.");
    }

    private static Finding RefreshTokenRotationFinding(FindingStatus status, string message, string? recommendation = null) =>
        new()
        {
            RuleId = "C09",
            RuleName = "Refresh Token Rotation",
            Status = status,
            Message = message,
            Recommendation = recommendation
        };

    // OAuth 2.1 §2.4: asymmetric client authentication (private_key_jwt or mTLS) is RECOMMENDED.
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
            Message = "Client uses shared secret authentication. OAuth 2.1 (§2.4) recommends asymmetric client authentication such as private_key_jwt or mTLS.",
            Recommendation = "Consider migrating to private_key_jwt or mTLS authentication for enhanced security."
        };
    }
}
