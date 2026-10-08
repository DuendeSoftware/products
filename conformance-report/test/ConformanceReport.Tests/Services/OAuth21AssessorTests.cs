// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.ConformanceReport.Internal.Models;

namespace Duende.ConformanceReport.Services;

public class OAuth21AssessorTests
{
    private static ConformanceReportServerOptions CreateDefaultServerOptions(
        bool parEnabled = true,
        bool parRequired = false,
        bool mtlsEnabled = false,
        bool allowUnregisteredPushedRedirectUris = false,
        IReadOnlyCollection<string>? dpopSigningAlgorithms = null,
        bool emitIssuerIdentificationResponseParameter = true,
        bool loopbackRedirectUrisEnabled = false) =>
        new()
        {
            PushedAuthorizationEndpointEnabled = parEnabled,
            PushedAuthorizationRequired = parRequired,
            PushedAuthorizationLifetime = 600,
            AllowUnregisteredPushedRedirectUris = allowUnregisteredPushedRedirectUris,
            LoopbackRedirectUrisEnabled = loopbackRedirectUrisEnabled,
            DiscoveryEndpointEnabled = true,
            MutualTlsEnabled = mtlsEnabled,
            SupportedClientAssertionSigningAlgorithms = ["RS256", "ES256"],
            SupportedRequestObjectSigningAlgorithms = ["RS256", "ES256"],
            DPoPSigningAlgorithms = dpopSigningAlgorithms ?? [],
            TokenSigningAlgorithms = ["RS256"],
            JwtValidationClockSkew = TimeSpan.FromMinutes(5),
            EmitIssuerIdentificationResponseParameter = emitIssuerIdentificationResponseParameter
        };

    private static ConformanceReportClient CreateDefaultClient(
        string clientId = "test-client",
        IReadOnlyCollection<string>? grantTypes = null,
        bool requirePkce = true,
        bool allowPlainTextPkce = false,
        IReadOnlyCollection<string>? redirectUris = null,
        bool requireClientSecret = true,
        IReadOnlyCollection<string>? secretTypes = null,
        bool requirePar = false,
        bool requireDPoP = false,
        ConformanceReportDPoPValidationMode dpopMode = ConformanceReportDPoPValidationMode.None,
        int authCodeLifetime = 60,
        bool allowOfflineAccess = true,
        ConformanceReportTokenUsage refreshTokenUsage = ConformanceReportTokenUsage.OneTimeOnly,
        bool allowAccessTokensViaBrowser = false,
        bool requireRequestObject = false) =>
        new()
        {
            ClientId = clientId,
            ClientName = "Test Client",
            AllowedGrantTypes = grantTypes ?? [ConformanceReportGrantTypes.AuthorizationCode],
            RequirePkce = requirePkce,
            AllowPlainTextPkce = allowPlainTextPkce,
            RedirectUris = redirectUris ?? ["https://example.com/callback"],
            RequireClientSecret = requireClientSecret,
            ClientSecretTypes = secretTypes ?? [ConformanceReportSecretTypes.SharedSecret],
            RequirePushedAuthorization = requirePar,
            RequireDPoP = requireDPoP,
            DPoPValidationMode = dpopMode,
            AuthorizationCodeLifetime = authCodeLifetime,
            AllowOfflineAccess = allowOfflineAccess,
            RefreshTokenUsage = refreshTokenUsage,
            AllowAccessTokensViaBrowser = allowAccessTokensViaBrowser,
            RequireRequestObject = requireRequestObject
        };

    private static Finding GetFinding(IReadOnlyList<Finding> findings, string ruleId)
        => findings.First(f => f.RuleId == ruleId);

    public class ServerAssessments
    {
        [Fact]
        public void S01_pkce_support_always_passes()
        {
            var options = CreateDefaultServerOptions();
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer([]);

            var finding = GetFinding(findings, "S01");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.RuleName.ShouldBe("PKCE Support");
            finding.Message.ShouldContain("PKCE");
        }

        [Fact]
        public void S02_no_client_uses_removed_grant_passes()
        {
            var options = CreateDefaultServerOptions();
            var assessor = new OAuth21Assessor(options);
            var clients = new[]
            {
                CreateDefaultClient(clientId: "client-1", grantTypes: [ConformanceReportGrantTypes.AuthorizationCode]),
                CreateDefaultClient(clientId: "client-2", grantTypes: [ConformanceReportGrantTypes.ClientCredentials])
            };

            var findings = assessor.AssessServer(clients);

            var finding = GetFinding(findings, "S02");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("No client uses a grant type removed by OAuth 2.1.");
        }

        [Fact]
        public void S02_client_with_password_grant_fails_and_names_client()
        {
            var options = CreateDefaultServerOptions();
            var assessor = new OAuth21Assessor(options);
            var clients = new[]
            {
                CreateDefaultClient(clientId: "legacy-client", grantTypes: [ConformanceReportGrantTypes.Password])
            };

            var findings = assessor.AssessServer(clients);

            var finding = GetFinding(findings, "S02");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("legacy-client");
            finding.Message.ShouldContain(ConformanceReportGrantTypes.Password);
        }

        [Fact]
        public void S02_client_with_implicit_grant_and_access_tokens_via_browser_fails_and_names_client()
        {
            var options = CreateDefaultServerOptions();
            var assessor = new OAuth21Assessor(options);
            var clients = new[]
            {
                CreateDefaultClient(
                    clientId: "spa-client",
                    grantTypes: [ConformanceReportGrantTypes.Implicit],
                    allowAccessTokensViaBrowser: true)
            };

            var findings = assessor.AssessServer(clients);

            var finding = GetFinding(findings, "S02");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("spa-client");
            finding.Message.ShouldContain(ConformanceReportGrantTypes.Implicit);
        }

        [Fact]
        public void S02_client_with_hybrid_grant_but_no_access_tokens_via_browser_does_not_fail()
        {
            var options = CreateDefaultServerOptions();
            var assessor = new OAuth21Assessor(options);
            var clients = new[]
            {
                CreateDefaultClient(
                    clientId: "hybrid-client",
                    grantTypes: [ConformanceReportGrantTypes.Hybrid],
                    allowAccessTokensViaBrowser: false)
            };

            var findings = assessor.AssessServer(clients);

            var finding = GetFinding(findings, "S02");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Theory]
        [InlineData(new string[] { "ES256" }, false, true)]
        [InlineData(new string[] { }, true, true)]
        [InlineData(new string[] { }, false, false)]
        public void S04_sender_constrained_token_support(string[] dpopAlgorithms, bool mtlsEnabled, bool expectedPass)
        {
            var options = CreateDefaultServerOptions(mtlsEnabled: mtlsEnabled, dpopSigningAlgorithms: dpopAlgorithms);
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer([]);

            var finding = GetFinding(findings, "S04");
            finding.Status.ShouldBe(expectedPass ? FindingStatus.Pass : FindingStatus.Warning);
            if (!expectedPass)
            {
                finding.Recommendation!.ShouldContain("SupportedDPoPSigningAlgorithms");
            }
        }

        [Fact]
        public void S08_http_303_redirects_always_passes()
        {
            var options = CreateDefaultServerOptions();
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer([]);

            var finding = GetFinding(findings, "S08");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.RuleName.ShouldBe("HTTP 303 Redirects");
            finding.Message.ShouldContain("303");
            finding.Message.ShouldContain("§7.5.4");
        }

        [Fact]
        public void S09_issuer_identification_enabled_passes()
        {
            var options = CreateDefaultServerOptions(emitIssuerIdentificationResponseParameter: true);
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer([]);

            var finding = GetFinding(findings, "S09");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.RuleName.ShouldBe("Issuer Identification");
        }

        [Fact]
        public void S09_issuer_identification_disabled_fails()
        {
            var options = CreateDefaultServerOptions(emitIssuerIdentificationResponseParameter: false);
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer([]);

            var finding = GetFinding(findings, "S09");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("iss");
            finding.Recommendation!.ShouldContain("EmitIssuerIdentificationResponseParameter = true");
        }
    }

    public class ClientAssessments
    {
        private readonly OAuth21Assessor _assessor = new(CreateDefaultServerOptions());

        [Theory]
        [InlineData("AuthorizationCode", FindingStatus.Pass)]
        [InlineData("ClientCredentials", FindingStatus.Pass)]
        [InlineData("RefreshToken", FindingStatus.Pass)]
        [InlineData("Password", FindingStatus.Fail)]
        [InlineData("DeviceCode", FindingStatus.Pass)]
        [InlineData("TokenExchange", FindingStatus.Pass)]
        [InlineData("Ciba", FindingStatus.Pass)]
        [InlineData("Custom", FindingStatus.Pass)]
        public void C01_grant_type_validation(string grantType, FindingStatus expectedStatus)
        {
            var grantTypes = grantType switch
            {
                "AuthorizationCode" => new[] { ConformanceReportGrantTypes.AuthorizationCode },
                "ClientCredentials" => new[] { ConformanceReportGrantTypes.ClientCredentials },
                "RefreshToken" => new[] { ConformanceReportGrantTypes.AuthorizationCode, ConformanceReportGrantTypes.RefreshToken },
                "Password" => new[] { ConformanceReportGrantTypes.Password },
                "DeviceCode" => new[] { ConformanceReportGrantTypes.DeviceCode },
                "TokenExchange" => new[] { "urn:ietf:params:oauth:grant-type:token-exchange" },
                "Ciba" => new[] { "urn:openid:params:grant-type:ciba" },
                "Custom" => new[] { "urn:my-company:custom-grant" },
                _ => throw new ArgumentException($"Unknown grant type: {grantType}")
            };

            var client = CreateDefaultClient(grantTypes: grantTypes);
            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C01");
            finding.Status.ShouldBe(expectedStatus);

            if (expectedStatus == FindingStatus.Fail)
            {
                if (grantType == "Password")
                {
                    finding.Message.ShouldContain("password");
                }
            }
        }

        [Theory]
        [InlineData("Implicit")]
        [InlineData("Hybrid")]
        public void C01_hybrid_with_access_tokens_via_browser_fails(string grantType)
        {
            var client = CreateDefaultClient(
                grantTypes: [grantType == "Implicit" ? ConformanceReportGrantTypes.Implicit : ConformanceReportGrantTypes.Hybrid],
                allowAccessTokensViaBrowser: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C01");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain(grantType.ToLowerInvariant());
        }

        [Theory]
        [InlineData("Implicit")]
        [InlineData("Hybrid")]
        public void C01_hybrid_without_access_tokens_via_browser_passes(string grantType)
        {
            var client = CreateDefaultClient(
                grantTypes: [grantType == "Implicit" ? ConformanceReportGrantTypes.Implicit : ConformanceReportGrantTypes.Hybrid],
                allowAccessTokensViaBrowser: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C01");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }


        [Fact]
        public void C02_PKCE_required_for_auth_code_passes()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePkce: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C02");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void C02_PKCE_not_required_for_auth_code_fails()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePkce: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C02");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("requires PKCE unless the client mitigates");
            finding.Recommendation!.ShouldContain("RequirePkce = true");
        }

        [Theory]
        [InlineData("C02", FindingStatus.Fail)]
        [InlineData("C03", FindingStatus.Fail)]
        [InlineData("C04", FindingStatus.Fail)]
        [InlineData("C08", FindingStatus.Warning)]
        public void code_flow_assessments_apply_to_hybrid(string ruleId, FindingStatus expectedStatus)
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.Hybrid],
                requirePkce: false,
                allowPlainTextPkce: true,
                redirectUris: ["callback"],
                authCodeLifetime: 900);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, ruleId);
            finding.Status.ShouldBe(expectedStatus);
        }

        [Theory]
        [InlineData("C02")]
        [InlineData("C03")]
        [InlineData("C04")]
        [InlineData("C08")]
        public void rule_not_applicable_for_client_credentials(string ruleId)
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.ClientCredentials]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, ruleId);
            finding.Status.ShouldBe(FindingStatus.NotApplicable);
        }

        [Fact]
        public void C03_plain_text_PKCE_disabled_passes()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                allowPlainTextPkce: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C03");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("S256");
        }

        [Fact]
        public void C03_plain_text_PKCE_enabled_fails()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                allowPlainTextPkce: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C03");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("Plain text PKCE is allowed");
            finding.Recommendation!.ShouldContain("AllowPlainTextPkce = false");
        }

        [Fact]
        public void C04_explicit_redirect_uri_passes()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["https://example.com/callback"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void C04_multiple_explicit_redirect_uris_passes()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["https://example.com/callback", "https://example.com/signin"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("2 redirect URI(s)");
        }

        [Fact]
        public void C04_no_redirect_uris_with_par_exemption_passes()
        {
            var assessor = new OAuth21Assessor(CreateDefaultServerOptions(allowUnregisteredPushedRedirectUris: true));
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: [],
                requireClientSecret: true);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("PAR");
        }

        [Fact]
        public void C04_no_redirect_uris_with_par_exemption_but_par_endpoint_disabled_fails()
        {
            var assessor = new OAuth21Assessor(CreateDefaultServerOptions(parEnabled: false, allowUnregisteredPushedRedirectUris: true));
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: [],
                requireClientSecret: true);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("PAR endpoint is disabled");
            finding.Recommendation!.ShouldContain("Enable the PAR endpoint");
        }

        [Fact]
        public void C04_no_redirect_uris_without_exemption_fails()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: []);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("No redirect URIs");
        }

        [Theory]
        [InlineData("https://*.example.com/callback")]
        [InlineData("https://app.*.example.com")]
        public void C04_wildcard_host_redirect_uri_fails_with_wildcard_message(string redirectUri)
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: [redirectUri]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("wildcard");
            finding.Message.ShouldNotContain("not an absolute URI");
        }

        [Fact]
        public void C04_relative_redirect_uri_fails()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["callback"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("not an absolute URI");
        }

        [Fact]
        public void C04_redirect_uri_with_fragment_fails()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["https://example.com/callback#fragment"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("fragment");
        }

        [Fact]
        public void C04_failure_includes_warnings_from_other_redirect_uris()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["http://evil.com/cb", "myapp://cb"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("http://evil.com/cb");
            finding.Message.ShouldContain("myapp://cb");
        }

        [Fact]
        public void C04_http_non_loopback_redirect_uri_fails()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["http://evil.com/cb"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("non-loopback host");
        }

        [Fact]
        public void C04_http_loopback_ip_redirect_uri_with_loopback_enabled_passes()
        {
            var assessor = new OAuth21Assessor(CreateDefaultServerOptions(loopbackRedirectUrisEnabled: true));
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["http://127.0.0.1/cb"]);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void C04_http_loopback_ip_redirect_uri_with_loopback_disabled_fails()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["http://127.0.0.1/cb"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("not configured for loopback redirection");
            finding.Recommendation!.ShouldContain("StrictRedirectUriValidatorAppAuth");
        }

        [Fact]
        public void C04_http_localhost_redirect_uri_warns()
        {
            var assessor = new OAuth21Assessor(CreateDefaultServerOptions(loopbackRedirectUrisEnabled: true));
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["http://localhost/cb"]);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Message.ShouldContain("localhost");
        }

        [Fact]
        public void C04_private_use_scheme_without_dot_warns()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["myapp://cb"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Warning);
        }

        [Fact]
        public void C04_private_use_reverse_domain_scheme_warns()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["com.example.app:/cb"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Message.ShouldContain("private-use URI scheme");
        }

        [Fact]
        public void C05_confidential_client_with_secret_passes()
        {
            var client = CreateDefaultClient(
                requireClientSecret: true,
                secretTypes: [ConformanceReportSecretTypes.SharedSecret]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C05");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("Confidential client has secrets");
        }

        [Fact]
        public void C05_confidential_client_no_secrets_fails()
        {
            var client = CreateDefaultClient(
                requireClientSecret: true,
                secretTypes: []);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C05");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("no secrets configured");
        }

        [Fact]
        public void C05_public_client_with_PKCE_passes()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requireClientSecret: false,
                requirePkce: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C05");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("Public client using a code flow with PKCE");
        }

        [Fact]
        public void C05_public_hybrid_client_with_PKCE_passes()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.Hybrid],
                requireClientSecret: false,
                requirePkce: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C05");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void C05_public_hybrid_client_without_PKCE_warns()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.Hybrid],
                requireClientSecret: false,
                requirePkce: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C05");
            finding.Status.ShouldBe(FindingStatus.Warning);
        }

        [Fact]
        public void C05_public_client_without_PKCE_warns()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requireClientSecret: false,
                requirePkce: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C05");
            finding.Status.ShouldBe(FindingStatus.Warning);
        }

        [Fact]
        public void C05_public_client_with_client_credentials_fails()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.ClientCredentials],
                requireClientSecret: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C05");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("only by confidential clients");
        }

        [Theory]
        [InlineData(ConformanceReportGrantTypes.DeviceCode)]
        [InlineData("urn:my-company:custom-grant")]
        public void C05_public_client_with_extension_grant_is_not_applicable(string grantType)
        {
            var client = CreateDefaultClient(
                grantTypes: [grantType],
                requireClientSecret: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C05");
            finding.Status.ShouldBe(FindingStatus.NotApplicable);
        }

        [Fact]
        public void C07_DPoP_bound_passes()
        {
            var assessor = new OAuth21Assessor(CreateDefaultServerOptions(dpopSigningAlgorithms: ["ES256"]));
            var client = CreateDefaultClient(requireDPoP: true);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "C07");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("DPoP");
        }

        [Fact]
        public void C07_DPoP_required_but_no_algorithms_warns()
        {
            var client = CreateDefaultClient(requireDPoP: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C07");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Message.ShouldContain("rejects every DPoP proof");
            finding.Recommendation!.ShouldContain("SupportedDPoPSigningAlgorithms");
            finding.Recommendation!.ShouldNotContain("Set RequireDPoP = true");
        }

        [Fact]
        public void C07_MTLS_certificate_passes()
        {
            var assessor = new OAuth21Assessor(CreateDefaultServerOptions(mtlsEnabled: true));
            var client = CreateDefaultClient(
                requireClientSecret: true,
                secretTypes: [ConformanceReportSecretTypes.X509CertificateThumbprint]);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "C07");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("mTLS");
        }

        [Fact]
        public void C07_MTLS_certificate_base64_not_mtls_bound_warns()
        {
            var assessor = new OAuth21Assessor(CreateDefaultServerOptions(mtlsEnabled: true));
            var client = CreateDefaultClient(
                requireClientSecret: true,
                secretTypes: [ConformanceReportSecretTypes.X509CertificateBase64]);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "C07");
            finding.Status.ShouldBe(FindingStatus.Warning);
        }

        [Fact]
        public void C07_no_sender_constraint_warns()
        {
            var client = CreateDefaultClient(
                requireDPoP: false,
                secretTypes: [ConformanceReportSecretTypes.SharedSecret]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C07");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Recommendation!.ShouldContain("Set RequireDPoP = true");
        }

        [Theory]
        [InlineData(300)]
        [InlineData(600)]
        public void C08_auth_code_lifetime_within_range_passes(int seconds)
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                authCodeLifetime: seconds);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C08");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Theory]
        [InlineData(601)]
        [InlineData(900)]
        public void C08_auth_code_lifetime_too_long_warns(int seconds)
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                authCodeLifetime: seconds);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C08");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Recommendation!.ShouldContain("reducing AuthorizationCodeLifetime");
        }

        [Fact]
        public void C09_confidential_client_not_applicable()
        {
            var client = CreateDefaultClient(
                requireClientSecret: true,
                allowOfflineAccess: true,
                refreshTokenUsage: ConformanceReportTokenUsage.ReUse);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C09");
            finding.Status.ShouldBe(FindingStatus.NotApplicable);
        }

        [Fact]
        public void C09_public_client_one_time_only_without_DPoP_warns()
        {
            var client = CreateDefaultClient(
                requireClientSecret: false,
                allowOfflineAccess: true,
                refreshTokenUsage: ConformanceReportTokenUsage.OneTimeOnly);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C09");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Message.ShouldContain("can't be verified from configuration");
            finding.Recommendation!.ShouldContain("DefaultRefreshTokenService");
        }

        [Fact]
        public void C09_public_client_DPoP_bound_passes()
        {
            var assessor = new OAuth21Assessor(CreateDefaultServerOptions(dpopSigningAlgorithms: ["ES256"]));
            var client = CreateDefaultClient(
                requireClientSecret: false,
                requireDPoP: true,
                allowOfflineAccess: true,
                refreshTokenUsage: ConformanceReportTokenUsage.ReUse);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "C09");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("DPoP-bound");
        }

        [Fact]
        public void C09_public_client_reuse_without_DPoP_fails()
        {
            var client = CreateDefaultClient(
                requireClientSecret: false,
                requireDPoP: false,
                allowOfflineAccess: true,
                refreshTokenUsage: ConformanceReportTokenUsage.ReUse);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C09");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("reusable");
            finding.Message.ShouldContain("§4.3.1");
            finding.Recommendation!.ShouldContain("SupportedDPoPSigningAlgorithms");
        }

        [Fact]
        public void C09_no_offline_access_not_applicable()
        {
            var client = CreateDefaultClient(allowOfflineAccess: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C09");
            finding.Status.ShouldBe(FindingStatus.NotApplicable);
        }

        [Theory]
        [InlineData("JsonWebKey", "private_key_jwt")]
        [InlineData("X509CertificateThumbprint", "mTLS")]
        [InlineData("X509CertificateName", null)]
        public void C11_secure_secret_types_passes(string secretType, string? expectedMessageSubstring)
        {
            var secretTypeValue = secretType switch
            {
                "JsonWebKey" => ConformanceReportSecretTypes.JsonWebKey,
                "X509CertificateThumbprint" => ConformanceReportSecretTypes.X509CertificateThumbprint,
                "X509CertificateName" => ConformanceReportSecretTypes.X509CertificateName,
                _ => throw new ArgumentException($"Unknown secret type: {secretType}")
            };

            var client = CreateDefaultClient(
                requireClientSecret: true,
                secretTypes: [secretTypeValue]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C11");
            finding.Status.ShouldBe(FindingStatus.Pass);

            if (expectedMessageSubstring != null)
            {
                finding.Message.ShouldContain(expectedMessageSubstring);
            }
        }

        [Fact]
        public void C11_shared_secret_warns()
        {
            var client = CreateDefaultClient(
                requireClientSecret: true,
                secretTypes: [ConformanceReportSecretTypes.SharedSecret]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C11");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Message.ShouldContain("shared secret");
            finding.Recommendation!.ShouldContain("private_key_jwt or mTLS");
        }

        [Fact]
        public void C11_public_client_not_applicable()
        {
            var client = CreateDefaultClient(requireClientSecret: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C11");
            finding.Status.ShouldBe(FindingStatus.NotApplicable);
            finding.Message.ShouldContain("public client");
        }
    }

    public class CompleteConfigurationTests
    {
        [Fact]
        public void OAuth21_compliant_server_has_all_passes()
        {
            var options = CreateDefaultServerOptions(emitIssuerIdentificationResponseParameter: true, dpopSigningAlgorithms: ["ES256"]);

            var assessor = new OAuth21Assessor(options);
            var findings = assessor.AssessServer([]);

            findings.Count.ShouldBe(5);
            findings.ShouldAllHaveStatus(FindingStatus.Pass);
        }

        [Fact]
        public void OAuth21_compliant_client_has_no_failures()
        {
            var options = CreateDefaultServerOptions(dpopSigningAlgorithms: ["ES256"]);
            var assessor = new OAuth21Assessor(options);

            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode, ConformanceReportGrantTypes.RefreshToken],
                requirePkce: true,
                allowPlainTextPkce: false,
                redirectUris: ["https://example.com/callback"],
                requireClientSecret: true,
                secretTypes: [ConformanceReportSecretTypes.JsonWebKey],
                requirePar: true,
                requireDPoP: true,
                dpopMode: ConformanceReportDPoPValidationMode.Nonce,
                authCodeLifetime: 300,
                allowOfflineAccess: true,
                refreshTokenUsage: ConformanceReportTokenUsage.OneTimeOnly);

            var findings = assessor.AssessClient(client);

            findings.ShouldNotBeEmpty();
            findings.Count.ShouldBe(9); // C01, C02, C03, C04, C05, C07, C08, C09, C11
            findings.ShouldAllHaveStatus(FindingStatus.Pass, FindingStatus.Warning, FindingStatus.NotApplicable);
            findings.ShouldNotContain(f => f.Status == FindingStatus.Fail);
        }

        [Fact]
        public void minimally_configured_client_has_multiple_warnings()
        {
            var options = CreateDefaultServerOptions();
            var assessor = new OAuth21Assessor(options);

            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePkce: true,
                allowPlainTextPkce: false,
                redirectUris: ["https://example.com/callback"],
                requireClientSecret: true,
                secretTypes: [ConformanceReportSecretTypes.SharedSecret],
                requirePar: false,
                requireDPoP: false,
                authCodeLifetime: 60,
                allowOfflineAccess: false);

            var findings = assessor.AssessClient(client);

            // Should have some warnings for: no sender constraint, shared secret auth
            var warnings = findings.Where(f => f.Status == FindingStatus.Warning).ToList();
            warnings.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void non_compliant_client_with_implicit_grant_fails()
        {
            var options = CreateDefaultServerOptions();
            var assessor = new OAuth21Assessor(options);

            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.Implicit],
                allowAccessTokensViaBrowser: true);

            var findings = assessor.AssessClient(client);

            var grantTypeFinding = GetFinding(findings, "C01");
            grantTypeFinding.Status.ShouldBe(FindingStatus.Fail);
        }
    }
}
