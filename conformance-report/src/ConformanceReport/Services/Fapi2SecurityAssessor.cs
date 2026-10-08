// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.ConformanceReport.Internal.Models;

namespace Duende.ConformanceReport.Services;

/// <summary>
/// Assesses configuration against the FAPI 2.0 Security Profile specification.
/// See: https://openid.net/specs/fapi-security-profile-2_0-final.html
/// </summary>
internal class Fapi2SecurityAssessor(ConformanceReportServerOptions options)
{
    // FAPI 2.0 Section 5.4.1 allows only PS256, ES256, or EdDSA (Ed25519). IdentityServer does not
    // support EdDSA, so PS256 and ES256 are the only algorithms that can satisfy this profile.
    private static readonly HashSet<string> Fapi2AllowedAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        SigningAlgorithms.RsaSsaPssSha256,
        SigningAlgorithms.EcdsaSha256,
        "PS256",
        "ES256"
    };

    // FAPI 2.0 requires PAR request_uri lifetimes to be less than 600 seconds (exclusive upper bound)
    private const int ParLifetimeExclusiveLimitSeconds = 600;

    private const string AllowedAlgorithmsDescription = "PS256 or ES256";

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
            // FS03: Only FAPI 2.0 algorithms for private_key_jwt client assertions
            AssessClientAssertionSigningAlgorithms(),
            // FS04: PAR lifetime must be less than 600 seconds
            AssessParLifetime(),
            // FS05: A sender-constraining mechanism (mTLS or DPoP) must be available
            AssessSenderConstrainingMechanisms(),
            // FS06: Issuer identification response parameter required
            AssessIssuerIdentification(),
            // FS07: HTTP 303 redirects (fixed IdentityServer behavior)
            AssessHttp303Redirects(),
            // FS08: PKCE with S256 is supported (fixed IdentityServer behavior)
            AssessPkceSupport(),
            // FS09: Only FAPI 2.0 algorithms for signing issued tokens
            AssessTokenSigningAlgorithms(),
            // FS10: Only FAPI 2.0 algorithms for DPoP proofs
            AssessDpopSigningAlgorithms(),
            // FS11: Discovery endpoint must be enabled
            AssessDiscoveryEndpoint(),
            // FS12: Issuer URI (if set) must use https
            AssessIssuerUriScheme(),
            // FS13: Only FAPI 2.0 algorithms for JAR request objects
            AssessRequestObjectSigningAlgorithms(),
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
            // FC01: No prohibited grants (implicit, hybrid, password)
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
            // FC08: Refresh token rotation discouraged if refresh tokens enabled
            AssessRefreshTokenRotation(client),
            // FC09: DPoP server-provided nonce usage (informational)
            AssessDPoPNonce(client),
            // FC10: Explicit redirect URIs (no wildcard hosts)
            AssessExplicitRedirectUris(client),
            // FC13: Redirect URI scheme (no http except loopback)
            AssessRedirectUriScheme(client)
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

    private Finding AssessClientAssertionSigningAlgorithms() => AssessAlgorithms(
            "FS03",
            "Client Assertion Signing Algorithms",
            "private_key_jwt client assertions",
            "IdentityServerOptions.SupportedClientAssertionSigningAlgorithms",
            options.SupportedClientAssertionSigningAlgorithms,
            "No algorithms are configured for private_key_jwt client assertions, so any algorithm is accepted.");

    private Finding AssessRequestObjectSigningAlgorithms() => AssessAlgorithms(
            "FS13",
            "Request Object Signing Algorithms",
            "JAR request objects",
            "IdentityServerOptions.SupportedRequestObjectSigningAlgorithms",
            options.SupportedRequestObjectSigningAlgorithms,
            "No algorithms are configured for JAR request objects, so any algorithm is accepted.");

    private Finding AssessTokenSigningAlgorithms() => AssessAlgorithms(
            "FS09",
            "Token Signing Algorithms",
            "signing issued tokens",
            "IdentityServerOptions.KeyManagement.SigningAlgorithms",
            options.TokenSigningAlgorithms,
            "No signing algorithms are configured for issued tokens, so tokens are signed with the default RS256.");

    private Finding AssessDpopSigningAlgorithms()
    {
        const string ruleId = "FS10";
        const string ruleName = "DPoP Signing Algorithms";
        const string optionName = "IdentityServerOptions.DPoP.SupportedDPoPSigningAlgorithms";

        if (options.DPoPSigningAlgorithms.Count == 0)
        {
            // An empty collection rejects every DPoP proof, which effectively disables DPoP.
            // FAPI 2.0 permits sender-constrained tokens via mTLS or DPoP, so mTLS alone is sufficient.
            if (options.MutualTlsEnabled)
            {
                return new Finding
                {
                    RuleId = ruleId,
                    RuleName = ruleName,
                    Status = FindingStatus.NotApplicable,
                    Message = "No DPoP proof algorithms are configured, so DPoP is effectively disabled. mTLS is enabled to provide sender-constrained tokens."
                };
            }

            return new Finding
            {
                RuleId = ruleId,
                RuleName = ruleName,
                Status = FindingStatus.Fail,
                Message = "No DPoP proof algorithms are configured, so all DPoP proofs are rejected, and mTLS is not enabled. FAPI 2.0 requires sender-constrained tokens via DPoP or mTLS.",
                Recommendation = $"Enable mTLS, or set {optionName} to only {AllowedAlgorithmsDescription} to enable DPoP."
            };
        }

        return AssessAlgorithms(
            ruleId,
            ruleName,
            "DPoP proofs",
            optionName,
            options.DPoPSigningAlgorithms,
            emptyMessage: null);
    }

    private static Finding AssessAlgorithms(
        string ruleId,
        string ruleName,
        string usage,
        string optionName,
        IReadOnlyCollection<string> algorithms,
        string? emptyMessage)
    {
        if (algorithms.Count == 0)
        {
            return new Finding
            {
                RuleId = ruleId,
                RuleName = ruleName,
                Status = FindingStatus.Fail,
                Message = $"{emptyMessage ?? $"No algorithms are configured for {usage}."} FAPI 2.0 Section 5.4.1 requires {AllowedAlgorithmsDescription}.",
                Recommendation = $"Set {optionName} to only {AllowedAlgorithmsDescription}."
            };
        }

        var nonFapiAlgorithms = algorithms
            .Where(a => !Fapi2AllowedAlgorithms.Contains(a))
            .ToList();

        if (nonFapiAlgorithms.Count == 0)
        {
            return new Finding
            {
                RuleId = ruleId,
                RuleName = ruleName,
                Status = FindingStatus.Pass,
                Message = $"All algorithms configured for {usage} are FAPI 2.0 compliant ({AllowedAlgorithmsDescription})."
            };
        }

        var hasFapiAlgorithm = algorithms.Any(a => Fapi2AllowedAlgorithms.Contains(a));

        return new Finding
        {
            RuleId = ruleId,
            RuleName = ruleName,
            Status = FindingStatus.Fail,
            Message = hasFapiAlgorithm
                ? $"Non-FAPI 2.0 algorithms are configured for {usage}: {string.Join(", ", nonFapiAlgorithms)}. FAPI 2.0 Section 5.4.1 allows {AllowedAlgorithmsDescription}."
                : $"No FAPI 2.0 compliant algorithms are configured for {usage}. Found: {string.Join(", ", algorithms)}. FAPI 2.0 Section 5.4.1 allows {AllowedAlgorithmsDescription}.",
            Recommendation = $"Set {optionName} to only {AllowedAlgorithmsDescription}."
        };
    }

    private Finding AssessParLifetime()
    {
        var lifetime = options.PushedAuthorizationLifetime;

        if (lifetime < ParLifetimeExclusiveLimitSeconds)
        {
            return new Finding
            {
                RuleId = "FS04",
                RuleName = "PAR Lifetime",
                Status = FindingStatus.Pass,
                Message = $"PAR lifetime is {lifetime} seconds, below the FAPI 2.0 limit of {ParLifetimeExclusiveLimitSeconds} seconds."
            };
        }

        return new Finding
        {
            RuleId = "FS04",
            RuleName = "PAR Lifetime",
            Status = FindingStatus.Fail,
            Message = $"PAR lifetime is {lifetime} seconds. FAPI 2.0 requires a lifetime of less than {ParLifetimeExclusiveLimitSeconds} seconds.",
            Recommendation = $"Set PushedAuthorization.Lifetime to less than {ParLifetimeExclusiveLimitSeconds} seconds (for example, {ParLifetimeExclusiveLimitSeconds - 1})."
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

    // FAPI 2.0 Security Profile: the authorization server shall distribute discovery
    // metadata (such as the authorization endpoint) via the metadata document as
    // specified in OIDC Discovery and RFC 8414.
    private Finding AssessDiscoveryEndpoint() =>
        new()
        {
            RuleId = "FS11",
            RuleName = "Discovery Endpoint",
            Status = options.DiscoveryEndpointEnabled ? FindingStatus.Pass : FindingStatus.Fail,
            Message = options.DiscoveryEndpointEnabled
                ? "Discovery endpoint is enabled."
                : "Discovery endpoint is not enabled. FAPI 2.0 requires the authorization server to publish its metadata via the discovery document (OIDC Discovery / RFC 8414).",
            Recommendation = options.DiscoveryEndpointEnabled
                ? null
                : "Set Endpoints.EnableDiscoveryEndpoint = true."
        };

    // RFC 8414 section 2 and OIDC Discovery section 3 require the issuer to be a URL
    // that uses the https scheme. FAPI 2.0 requires metadata per these specs.
    private Finding AssessIssuerUriScheme()
    {
        const string ruleId = "FS12";
        const string ruleName = "Issuer URI Scheme";

        if (string.IsNullOrWhiteSpace(options.IssuerUri))
        {
            return new Finding
            {
                RuleId = ruleId,
                RuleName = ruleName,
                Status = FindingStatus.Pass,
                Message = "IssuerUri is not set. The issuer is inferred from the request."
            };
        }

        if (Uri.TryCreate(options.IssuerUri, UriKind.Absolute, out var issuer) &&
            string.Equals(issuer.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return new Finding
            {
                RuleId = ruleId,
                RuleName = ruleName,
                Status = FindingStatus.Pass,
                Message = $"IssuerUri '{options.IssuerUri}' uses the https scheme."
            };
        }

        return new Finding
        {
            RuleId = ruleId,
            RuleName = ruleName,
            Status = FindingStatus.Fail,
            Message = $"IssuerUri '{options.IssuerUri}' is not an absolute https URL. RFC 8414 and OIDC Discovery require the issuer to use the https scheme.",
            Recommendation = "Set IssuerUri to an https URL, or leave it unset so the issuer is inferred from the request."
        };
    }

    // FAPI 2.0 Security Profile §5.3.2.2: the authorization server shall not use HTTP 307 when
    // redirecting a request that contains user credentials, and should use HTTP 303.
    // IdentityServer always uses HTTP 303 (See Other) for redirects.
    private static Finding AssessHttp303Redirects() =>
        new()
        {
            RuleId = "FS07",
            RuleName = "HTTP 303 Redirects",
            Status = FindingStatus.Pass,
            Message = "IdentityServer always uses HTTP 303 (See Other) for redirects. FAPI 2.0 Section 5.3.2.2 prohibits HTTP 307 for redirects that contain user credentials and recommends HTTP 303."
        };

    // FAPI 2.0 Security Profile §5.3.2.1: the authorization server shall only issue
    // sender-constrained access tokens, using mTLS (RFC 8705) or DPoP (RFC 9449).
    // This server-level rule records which mechanisms are available. FC05 checks that
    // each client actually uses one of them.
    // When mTLS is disabled and no DPoP algorithms are configured, both this rule and FS10 fail
    // on purpose: FS05 reports that no mechanism is available, and FS10 reports the DPoP
    // algorithm configuration.
    private Finding AssessSenderConstrainingMechanisms()
    {
        const string ruleId = "FS05";
        const string ruleName = "Sender-Constraining Mechanisms";

        var mtlsAvailable = options.MutualTlsEnabled;
        // An empty DPoP algorithm collection rejects every DPoP proof, so DPoP is not available.
        var dpopAvailable = options.DPoPSigningAlgorithms.Count > 0;

        if (!mtlsAvailable && !dpopAvailable)
        {
            return new Finding
            {
                RuleId = ruleId,
                RuleName = ruleName,
                Status = FindingStatus.Fail,
                Message = "Neither mTLS nor DPoP is available: mTLS is not enabled, and no DPoP proof algorithms are configured. FAPI 2.0 Section 5.3.2.1 requires sender-constrained access tokens via mTLS or DPoP.",
                Recommendation = $"Enable mTLS, or set IdentityServerOptions.DPoP.SupportedDPoPSigningAlgorithms to only {AllowedAlgorithmsDescription}."
            };
        }

        var mechanisms = new List<string>();
        if (mtlsAvailable)
        {
            mechanisms.Add("mTLS");
        }

        if (dpopAvailable)
        {
            mechanisms.Add("DPoP");
        }

        return new Finding
        {
            RuleId = ruleId,
            RuleName = ruleName,
            Status = FindingStatus.Pass,
            Message = $"Sender-constrained access tokens are available via {string.Join(" and ", mechanisms)}. Each client's use of a sender-constraining mechanism is assessed by FC05."
        };
    }

    // FAPI 2.0 Security Profile §5.3.2.2: the authorization server shall require PKCE with
    // S256 as the code challenge method. IdentityServer always supports S256. Each client's
    // PKCE requirement (RequirePkce, AllowPlainTextPkce) is assessed by FC03.
    private static Finding AssessPkceSupport() =>
        new()
        {
            RuleId = "FS08",
            RuleName = "PKCE Support",
            Status = FindingStatus.Pass,
            Message = "IdentityServer always supports PKCE with the S256 code challenge method. Each client's PKCE requirement is assessed by FC03."
        };

    private static Finding AssessGrantType(ConformanceReportClient client)
    {
        // FAPI 2.0 Section 5.3.1.1 prohibits the password, implicit, and hybrid grants.
        // Other grants (client_credentials, device_code, CIBA, etc.) may be supported.
        var prohibitedGrants = new HashSet<string>
        {
            ConformanceReportGrantTypes.Implicit,
            ConformanceReportGrantTypes.Hybrid,
            ConformanceReportGrantTypes.Password
        };

        var disallowedGrants = client.AllowedGrantTypes
            .Where(prohibitedGrants.Contains)
            .ToList();

        if (disallowedGrants.Count == 0)
        {
            return new Finding
            {
                RuleId = "FC01",
                RuleName = "FAPI 2.0 Grant Types",
                Status = FindingStatus.Pass,
                Message = $"Client does not use grant types prohibited by FAPI 2.0: {string.Join(", ", client.AllowedGrantTypes)}."
            };
        }

        return new Finding
        {
            RuleId = "FC01",
            RuleName = "FAPI 2.0 Grant Types",
            Status = FindingStatus.Fail,
            Message = $"Client uses grant types prohibited by FAPI 2.0: {string.Join(", ", disallowedGrants)}. FAPI 2.0 Section 5.3.1.1 prohibits the implicit, hybrid, and password grants.",
            Recommendation = "Remove the implicit, hybrid, and password grants."
        };
    }

    private static Finding AssessConfidentialClient(ConformanceReportClient client) =>
        new()
        {
            RuleId = "FC02",
            RuleName = "Confidential Client",
            Status = client.RequireClientSecret ? FindingStatus.Pass : FindingStatus.Fail,
            Message = client.RequireClientSecret
                ? "Client is configured as confidential (requires secret)."
                : "Client is configured as public (no secret required). FAPI 2.0 requires confidential clients.",
            Recommendation = client.RequireClientSecret ? null : "Set RequireClientSecret = true and configure appropriate client authentication."
        };

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

    private Finding AssessSenderConstrainedTokens(ConformanceReportClient client)
    {
        // See SenderConstraint for the shared DPoP-bound / mTLS-bound definitions used by
        // both this FAPI 2.0 assessor (FC05, Fail) and the OAuth 2.1 assessor (C07, Warning).
        var dpopBound = SenderConstraint.IsDPoPBound(options, client);
        var mtlsBound = SenderConstraint.IsMtlsBound(options, client);

        if (mtlsBound || dpopBound)
        {
            return new Finding
            {
                RuleId = "FC05",
                RuleName = "Sender-Constrained Tokens",
                Status = FindingStatus.Pass,
                Message = $"Client uses sender-constrained tokens via {(dpopBound ? "DPoP" : "")}{(dpopBound && mtlsBound ? " and " : "")}{(mtlsBound ? "mTLS" : "")}."
            };
        }

        return new Finding
        {
            RuleId = "FC05",
            RuleName = "Sender-Constrained Tokens",
            Status = FindingStatus.Fail,
            Message = client.RequireDPoP
                ? "Client requires DPoP, but no DPoP proof algorithms are configured, so the server rejects every DPoP proof. The client does not use mTLS either. FAPI 2.0 requires mTLS or DPoP."
                : "Client does not use sender-constrained tokens. FAPI 2.0 requires mTLS or DPoP.",
            Recommendation = client.RequireDPoP
                ? "Set IdentityServerOptions.DPoP.SupportedDPoPSigningAlgorithms to only PS256 or ES256, or enable mTLS and ensure that all client secrets are the X509 thumbprint or name secrets used by mTLS."
                : "Set RequireDPoP = true, or enable mTLS and ensure that all client secrets are the X509 thumbprint or name secrets used by mTLS."
        };
    }

    private static Finding AssessClientAuthentication(ConformanceReportClient client)
    {
        // FAPI 2.0 permits only private_key_jwt or mTLS, so every secret must map to one of those.
        var disallowed = client.ClientSecretTypes
            .Where(s => !IsPrivateKeyJwtSecret(s) && !IsMtlsSecret(s))
            .Distinct()
            .ToList();

        if (disallowed.Count > 0)
        {
            return new Finding
            {
                RuleId = "FC06",
                RuleName = "Secure Client Authentication",
                Status = FindingStatus.Fail,
                Message = $"Client has secrets that are not private_key_jwt or mTLS: {string.Join(", ", disallowed)}. FAPI 2.0 permits only private_key_jwt or mTLS.",
                Recommendation = "Remove all other secrets and configure client authentication using only private_key_jwt (JsonWebKey secret) or mTLS (X509Certificate secret)."
            };
        }

        var hasPrivateKeyJwt = client.ClientSecretTypes.Any(IsPrivateKeyJwtSecret);
        var hasMtls = client.ClientSecretTypes.Any(IsMtlsSecret);

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
            Message = "Client has no private_key_jwt or mTLS secret configured. FAPI 2.0 requires private_key_jwt or mTLS.",
            Recommendation = "Configure client authentication using private_key_jwt (JsonWebKey secret) or mTLS (X509Certificate secret)."
        };
    }

    private static bool IsPrivateKeyJwtSecret(string secretType) =>
        secretType is ConformanceReportSecretTypes.JsonWebKey or ConformanceReportSecretTypes.X509CertificateBase64;

    private static bool IsMtlsSecret(string secretType) =>
        secretType is ConformanceReportSecretTypes.X509CertificateThumbprint or ConformanceReportSecretTypes.X509CertificateName;

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
                RuleName = "Refresh Token Rotation Discouraged",
                Status = FindingStatus.NotApplicable,
                Message = "Client does not support refresh tokens (AllowOfflineAccess = false)."
            };
        }

        if (client.RefreshTokenUsage == ConformanceReportTokenUsage.ReUse)
        {
            return new Finding
            {
                RuleId = "FC08",
                RuleName = "Refresh Token Rotation Discouraged",
                Status = FindingStatus.Pass,
                Message = "Refresh tokens are reusable, as FAPI 2.0 prefers."
            };
        }

        return new Finding
        {
            RuleId = "FC08",
            RuleName = "Refresh Token Rotation Discouraged",
            Status = FindingStatus.Warning,
            Message = "Refresh token rotation is enabled (one-time use). FAPI 2.0 discourages refresh token rotation.",
            Recommendation = "Set RefreshTokenUsage = TokenUsage.ReUse."
        };
    }

    // FAPI 2.0 Security Profile §5.3.2.1: if using DPoP, the authorization server "may use"
    // the server-provided nonce mechanism (RFC 9449 Section 8). §5.3.3.1 requires FAPI 2.0
    // clients to support it. Nonces are optional for the server, so this rule records the
    // client's configuration for audit purposes and never fails.
    private Finding AssessDPoPNonce(ConformanceReportClient client)
    {
        const string ruleId = "FC09";
        const string ruleName = "DPoP Nonce";

        if (!SenderConstraint.IsDPoPBound(options, client))
        {
            return new Finding
            {
                RuleId = ruleId,
                RuleName = ruleName,
                Status = FindingStatus.NotApplicable,
                Message = "Client does not use DPoP-bound tokens."
            };
        }

        var nonceEnabled = client.DPoPValidationMode.HasFlag(ConformanceReportDPoPValidationMode.Nonce);

        return new Finding
        {
            RuleId = ruleId,
            RuleName = ruleName,
            Status = FindingStatus.Pass,
            Message = nonceEnabled
                ? "DPoP server-provided nonce validation is enabled for this client."
                : "DPoP server-provided nonce validation is not enabled for this client. FAPI 2.0 Section 5.3.2.1 makes server-provided nonces optional for the authorization server."
        };
    }

    // FAPI 2.0 follows the OAuth Security BCP (RFC 9700), which requires exact string matching of
    // redirect URIs (Section 4.1.3). IdentityServer compares redirect URIs by exact string match,
    // so a '*' in the path or query is matched literally and is not a wildcard. A '*' in the host
    // can never match a real redirect, so it is reported as a failure. This matches OAuth 2.1 C04.
    private static Finding AssessExplicitRedirectUris(ConformanceReportClient client)
    {
        const string ruleId = "FC10";
        const string ruleName = "Explicit Redirect URIs";

        if (!client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode))
        {
            return new Finding
            {
                RuleId = ruleId,
                RuleName = ruleName,
                Status = FindingStatus.NotApplicable,
                Message = "Client does not use authorization_code grant."
            };
        }

        if (client.RedirectUris.Count == 0)
        {
            return new Finding
            {
                RuleId = ruleId,
                RuleName = ruleName,
                Status = FindingStatus.NotApplicable,
                Message = "No redirect URIs are registered. FC13 assesses whether that is permitted."
            };
        }

        var wildcardHostUris = client.RedirectUris.Where(RedirectUriWildcards.HasWildcardHost).ToList();

        if (wildcardHostUris.Count > 0)
        {
            return new Finding
            {
                RuleId = ruleId,
                RuleName = ruleName,
                Status = FindingStatus.Fail,
                Message = $"Redirect URIs have a wildcard ('*') in the host: {string.Join(", ", wildcardHostUris)}. IdentityServer compares redirect URIs by exact string match and doesn't support wildcards, so these URIs can never match a real redirect. FAPI 2.0 follows RFC 9700, which requires exact redirect URI matching (Section 4.1.3).",
                Recommendation = "Replace each wildcard-host redirect URI with the complete, explicit redirect URI."
            };
        }

        return new Finding
        {
            RuleId = ruleId,
            RuleName = ruleName,
            Status = FindingStatus.Pass,
            Message = $"All {client.RedirectUris.Count} redirect URI(s) are explicit. IdentityServer matches redirect URIs by exact string comparison."
        };
    }

    private const string RedirectUriSchemeRuleId = "FC13";
    private const string RedirectUriSchemeRuleName = "Redirect URI Scheme";

    private Finding AssessRedirectUriScheme(ConformanceReportClient client)
    {
        if (!client.AllowedGrantTypes.Contains(ConformanceReportGrantTypes.AuthorizationCode))
        {
            return RedirectUriSchemeFinding(FindingStatus.NotApplicable, "Client does not use authorization_code grant.");
        }

        if (client.RedirectUris.Count == 0)
        {
            return AssessMissingRedirectUris(client);
        }

        // Wildcard-host URIs can't be parsed as absolute URIs. FC10 reports them, so they are
        // skipped here instead of being reported again as malformed.
        var assessedUris = client.RedirectUris.Where(u => !RedirectUriWildcards.HasWildcardHost(u)).ToList();
        var invalidUris = assessedUris.Where(u => !Uri.TryCreate(u, UriKind.Absolute, out _)).ToList();
        if (assessedUris.Count == 0)
        {
            return RedirectUriSchemeFinding(
                FindingStatus.NotApplicable,
                "All registered redirect URIs have a wildcard host. FC10 assesses them.");
        }

        var validUris = assessedUris.Except(invalidUris).Select(u => new Uri(u, UriKind.Absolute)).ToList();
        var httpLoopbackUris = validUris.Where(u => IsHttp(u) && u.IsLoopback).ToList();

        var problems = GetRedirectUriProblems(invalidUris, validUris, httpLoopbackUris);
        if (problems.Count > 0)
        {
            return RedirectUriProblemsFinding(problems);
        }

        // RFC 8252 section 8.3: use of "localhost" for loopback redirection is NOT RECOMMENDED.
        var localhostUris = httpLoopbackUris.Where(IsLocalhost).ToList();
        if (localhostUris.Count > 0)
        {
            return LocalhostRedirectUrisFinding(localhostUris);
        }

        return RedirectUriSchemeFinding(
            FindingStatus.Pass,
            $"All {assessedUris.Count} assessed redirect URI(s) use allowed schemes (no non-loopback http).");
    }

    private Finding AssessMissingRedirectUris(ConformanceReportClient client)
    {
        if (options.AllowUnregisteredPushedRedirectUris && client.RequireClientSecret)
        {
            // The exemption only applies to redirect URIs sent in a PAR request.
            if (!options.PushedAuthorizationEndpointEnabled)
            {
                return RedirectUriSchemeFinding(
                    FindingStatus.Fail,
                    "No redirect URIs are registered, and the PAR endpoint is disabled, so the client can't supply one. AllowUnregisteredPushedRedirectUris only applies to redirect URIs sent in a PAR request.",
                    "Enable the PAR endpoint, or configure at least one redirect URI.");
            }

            return RedirectUriSchemeFinding(
                FindingStatus.Pass,
                "No redirect URIs are registered. The client supplies redirect URIs through PAR (AllowUnregisteredPushedRedirectUris is enabled).");
        }

        return RedirectUriSchemeFinding(
            FindingStatus.Fail,
            "No redirect URIs configured.",
            "Configure at least one redirect URI.");
    }

    private List<string> GetRedirectUriProblems(
        List<string> invalidUris,
        List<Uri> validUris,
        List<Uri> httpLoopbackUris)
    {
        var problems = new List<string>();

        if (invalidUris.Count > 0)
        {
            problems.Add($"Malformed redirect URIs (not absolute URIs): {string.Join(", ", invalidUris)}.");
        }

        var httpNonLoopbackUris = validUris.Where(u => IsHttp(u) && !u.IsLoopback).ToList();
        if (httpNonLoopbackUris.Count > 0)
        {
            problems.Add($"Insecure redirect URIs detected: {JoinUris(httpNonLoopbackUris)}. FAPI 2.0 Security Profile section 5.3.1.2 prohibits redirect URIs using the http scheme except for loopback interface redirection (RFC 8252 section 7.3).");
        }

        // FAPI 2.0 Security Profile 5.3.1.2: the AS "shall not allow redirect URIs that use the
        // 'http' scheme except for native clients that use loopback interface redirection"
        // (RFC 8252 section 7.3). Loopback http is only accepted if the server has opted in to
        // loopback redirection for native clients.
        if (!options.LoopbackRedirectUrisEnabled && httpLoopbackUris.Count > 0)
        {
            problems.Add($"Loopback http redirect URIs detected: {JoinUris(httpLoopbackUris)}. FAPI 2.0 Security Profile section 5.3.1.2 permits http only for native clients using loopback interface redirection, but the server is not configured for loopback redirection.");
        }

        return problems;
    }

    private static Finding RedirectUriProblemsFinding(List<string> problems) =>
        RedirectUriSchemeFinding(
            FindingStatus.Fail,
            string.Join(" ", problems),
            "Register only absolute URIs. Use https redirect URIs. Use http only with a loopback IP address (127.0.0.1) for native clients, and register StrictRedirectUriValidatorAppAuth (AddAppAuthRedirectUriValidator).");

    private static Finding LocalhostRedirectUrisFinding(List<Uri> localhostUris) =>
        RedirectUriSchemeFinding(
            FindingStatus.Warning,
            $"Loopback redirect URIs use \"localhost\": {JoinUris(localhostUris)}. RFC 8252 section 8.3 does not recommend \"localhost\" for loopback redirection, because it can resolve to a non-loopback interface.",
            "Use the loopback IP literal (http://127.0.0.1) instead of localhost.");

    private static bool IsLocalhost(Uri uri) =>
        string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);

    private static Finding RedirectUriSchemeFinding(FindingStatus status, string message, string? recommendation = null) =>
        new()
        {
            RuleId = RedirectUriSchemeRuleId,
            RuleName = RedirectUriSchemeRuleName,
            Status = status,
            Message = message,
            Recommendation = recommendation
        };

    private static string JoinUris(IEnumerable<Uri> uris) => string.Join(", ", uris.Select(u => u.OriginalString));

    private static bool IsHttp(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
}
