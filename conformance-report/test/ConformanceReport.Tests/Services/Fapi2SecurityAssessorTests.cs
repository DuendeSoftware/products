// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.ConformanceReport.Internal.Models;

namespace Duende.ConformanceReport.Services;

public class Fapi2SecurityAssessorTests
{
    private static string[] AllowedAlgorithms = ["PS256", "ES256"];
    private static ConformanceReportServerOptions CreateDefaultServerOptions(
        bool parEnabled = true,
        bool parRequired = true,
        int parLifetime = 600,
        bool mtlsEnabled = true,
        IReadOnlyCollection<string>? signingAlgorithms = null,
        IReadOnlyCollection<string>? requestObjectSigningAlgorithms = null,
        IReadOnlyCollection<string>? tokenSigningAlgorithms = null,
        IReadOnlyCollection<string>? dpopSigningAlgorithms = null,
        TimeSpan? clockSkew = null,
        bool emitIssuer = true,
        bool allowUnregisteredPushedRedirectUris = false,
        bool loopbackRedirectUrisEnabled = false,
        bool discoveryEnabled = true) =>
        new()
        {
            PushedAuthorizationEndpointEnabled = parEnabled,
            PushedAuthorizationRequired = parRequired,
            PushedAuthorizationLifetime = parLifetime,
            AllowUnregisteredPushedRedirectUris = allowUnregisteredPushedRedirectUris,
            LoopbackRedirectUrisEnabled = loopbackRedirectUrisEnabled,
            DiscoveryEndpointEnabled = discoveryEnabled,
            MutualTlsEnabled = mtlsEnabled,
            SupportedClientAssertionSigningAlgorithms = signingAlgorithms ?? AllowedAlgorithms,
            SupportedRequestObjectSigningAlgorithms = requestObjectSigningAlgorithms ?? AllowedAlgorithms,
            TokenSigningAlgorithms = tokenSigningAlgorithms ?? AllowedAlgorithms,
            DPoPSigningAlgorithms = dpopSigningAlgorithms ?? AllowedAlgorithms,
            JwtValidationClockSkew = clockSkew ?? TimeSpan.FromMinutes(5),
            EmitIssuerIdentificationResponseParameter = emitIssuer
        };

    private static ConformanceReportClient CreateFapi2CompliantClient(
        string clientId = "fapi-client",
        IReadOnlyCollection<string>? grantTypes = null,
        bool requirePkce = true,
        bool allowPlainTextPkce = false,
        IReadOnlyCollection<string>? redirectUris = null,
        bool requireClientSecret = true,
        IReadOnlyCollection<string>? secretTypes = null,
        bool requirePar = true,
        bool requireDPoP = true,
        ConformanceReportDPoPValidationMode dpopMode = ConformanceReportDPoPValidationMode.Nonce,
        int authCodeLifetime = 60,
        bool allowOfflineAccess = true,
        ConformanceReportTokenUsage refreshTokenUsage = ConformanceReportTokenUsage.OneTimeOnly,
        bool allowAccessTokensViaBrowser = false,
        bool requireRequestObject = false) =>
        new()
        {
            ClientId = clientId,
            ClientName = "FAPI 2.0 Client",
            AllowedGrantTypes = grantTypes ?? [ConformanceReportGrantTypes.AuthorizationCode],
            RequirePkce = requirePkce,
            AllowPlainTextPkce = allowPlainTextPkce,
            RedirectUris = redirectUris ?? ["https://example.com/callback"],
            RequireClientSecret = requireClientSecret,
            ClientSecretTypes = secretTypes ?? [ConformanceReportSecretTypes.JsonWebKey],
            RequirePushedAuthorization = requirePar,
            RequireDPoP = requireDPoP,
            DPoPValidationMode = dpopMode,
            AuthorizationCodeLifetime = authCodeLifetime,
            AllowOfflineAccess = allowOfflineAccess,
            RefreshTokenUsage = refreshTokenUsage,
            AllowAccessTokensViaBrowser = allowAccessTokensViaBrowser,
            RequireRequestObject = requireRequestObject
        };

    private static Finding GetFinding(IReadOnlyList<Finding> findings, string ruleId) => findings.First(f => f.RuleId == ruleId);

    public class ServerAssessments
    {
        [Fact]
        public void FS01_PAR_enabled_and_required_passes()
        {
            var options = CreateDefaultServerOptions(parEnabled: true, parRequired: true);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS01");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("required globally");
        }

        [Fact]
        public void FS01_PAR_enabled_not_required_warns()
        {
            var options = CreateDefaultServerOptions(parEnabled: true, parRequired: false);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS01");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Message.ShouldContain("not required globally");
            _ = finding.Recommendation.ShouldNotBeNull();
        }

        [Fact]
        public void FS01_PAR_disabled_fails()
        {
            var options = CreateDefaultServerOptions(parEnabled: false, parRequired: false);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS01");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("not enabled");
        }

        public sealed class AlgorithmRule
        {
            internal AlgorithmRule(
                string ruleId,
                string subject,
                string recommendedOption,
                Func<IReadOnlyCollection<string>, IReadOnlyCollection<string>?, ConformanceReportServerOptions> configure)
            {
                RuleId = ruleId;
                Subject = subject;
                RecommendedOption = recommendedOption;
                Configure = configure;
            }

            public string RuleId { get; }

            public string Subject { get; }

            public string RecommendedOption { get; }

            internal Func<IReadOnlyCollection<string>, IReadOnlyCollection<string>?, ConformanceReportServerOptions> Configure { get; }

            public override string ToString() => RuleId;
        }

        public static readonly AlgorithmRule FS03 = new(
            "FS03",
            "client assertions",
            "SupportedClientAssertionSigningAlgorithms",
            (target, others) => CreateDefaultServerOptions(
                signingAlgorithms: target,
                requestObjectSigningAlgorithms: others,
                tokenSigningAlgorithms: others));

        public static readonly AlgorithmRule FS13 = new(
            "FS13",
            "request objects",
            "SupportedRequestObjectSigningAlgorithms",
            (target, others) => CreateDefaultServerOptions(
                signingAlgorithms: others,
                requestObjectSigningAlgorithms: target,
                tokenSigningAlgorithms: others));

        public static readonly AlgorithmRule FS09 = new(
            "FS09",
            "issued tokens",
            "KeyManagement.SigningAlgorithms",
            (target, others) => CreateDefaultServerOptions(
                signingAlgorithms: others,
                requestObjectSigningAlgorithms: others,
                tokenSigningAlgorithms: target));

        public static readonly AlgorithmRule FS10 = new(
            "FS10",
            "DPoP Proofs",
            "DPoP.SupportedDPoPSigningAlgorithms",
            (target, others) => CreateDefaultServerOptions(
                signingAlgorithms: others,
                requestObjectSigningAlgorithms: others,
                dpopSigningAlgorithms: target,
                tokenSigningAlgorithms: others));

        internal static readonly AlgorithmRule[] AllAlgorithmRules = [FS03, FS09, FS10, FS13];

        public static TheoryData<AlgorithmRule> AlgorithmRules => new(AllAlgorithmRules);

        public static MatrixTheoryData<AlgorithmRule, string> AlgorithmRulesWithCompliantAlgorithms =>
            new(AllAlgorithmRules, ["PS256", "ES256", "PS256,ES256"]);

        public static MatrixTheoryData<AlgorithmRule, string> AlgorithmRulesWithLongerHashVariants =>
            new(AllAlgorithmRules, ["PS384", "PS512", "ES384", "ES512"]);

        public static Finding AssessAlgorithmRule(
            AlgorithmRule rule,
            IReadOnlyCollection<string> algorithms,
            IReadOnlyCollection<string>? otherAlgorithms = null)
        {
            var assessor = new Fapi2SecurityAssessor(rule.Configure(algorithms, otherAlgorithms));

            var findings = assessor.AssessServer();

            return GetFinding(findings, rule.RuleId);
        }

        [Theory]
        [MemberData(nameof(AlgorithmRulesWithCompliantAlgorithms))]
        public void FAPI_compliant_algorithms_passes(AlgorithmRule rule, string algorithms)
        {
            var finding = AssessAlgorithmRule(rule, algorithms.Split(','));

            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Theory]
        [MemberData(nameof(AlgorithmRulesWithLongerHashVariants))]
        public void Signing_algorithm_rule_longer_hash_variants_are_not_FAPI_compliant(AlgorithmRule rule, string algorithmWithLongHashVariant)
        {
            var finding = AssessAlgorithmRule(rule, ["PS256", algorithmWithLongHashVariant]);

            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain(algorithmWithLongHashVariant);
        }

        [Theory]
        [MemberData(nameof(AlgorithmRules))]
        public void Signing_algorithm_rule_RS256_mixed_with_FAPI_fails(AlgorithmRule rule)
        {
            var finding = AssessAlgorithmRule(rule, ["PS256", "RS256"]);

            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("RS256");
            finding.Message.ShouldContain(rule.Subject);
        }

        [Theory]
        [MemberData(nameof(AlgorithmRules))]
        public void Signing_algorithm_rule_only_non_FAPI_algorithms_fails(AlgorithmRule rule)
        {
            var finding = AssessAlgorithmRule(rule, ["RS256", "HS256"]);

            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain(rule.Subject);
            finding.Recommendation.ShouldNotBeNull().ShouldContain(rule.RecommendedOption);
        }

        [Theory]
        [MemberData(nameof(AlgorithmRules))]
        public void Signing_algorithm_rule_RS256_only_fails(AlgorithmRule rule)
        {
            var finding = AssessAlgorithmRule(rule, ["RS256"]);

            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Recommendation.ShouldNotBeNull().ShouldContain(rule.RecommendedOption);
        }

        public static TheoryData<AlgorithmRule> AnyAlgorithmAcceptedWhenEmptyRules => new(FS03, FS13);

        [Theory]
        [MemberData(nameof(AnyAlgorithmAcceptedWhenEmptyRules))]
        public void Signing_algorithm_rule_empty_algorithms_accepts_any_algorithm_fails(AlgorithmRule rule)
        {
            var finding = AssessAlgorithmRule(rule, []);

            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("so any algorithm is accepted");
            finding.Message.ShouldContain(rule.Subject);
            finding.Recommendation.ShouldNotBeNull().ShouldContain(rule.RecommendedOption);
        }

        [Fact]
        public void FS09_empty_algorithms_falls_back_to_RS256_fails()
        {
            var finding = AssessAlgorithmRule(FS09, []);

            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("default RS256");
            finding.Message.ShouldNotContain("any algorithm is accepted");
            finding.Recommendation.ShouldNotBeNull().ShouldContain(FS09.RecommendedOption);
        }

        [Fact]
        public void FS10_empty_algorithms_with_mTLS_is_not_applicable()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions(mtlsEnabled: true, dpopSigningAlgorithms: []));

            var finding = GetFinding(assessor.AssessServer(), "FS10");

            finding.Status.ShouldBe(FindingStatus.NotApplicable);
            finding.Message.ShouldContain("DPoP is effectively disabled");
            finding.Message.ShouldNotContain("any algorithm is accepted");
        }

        [Fact]
        public void FS10_empty_algorithms_without_mTLS_fails()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions(mtlsEnabled: false, dpopSigningAlgorithms: []));

            var finding = GetFinding(assessor.AssessServer(), "FS10");

            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("all DPoP proofs are rejected");
            finding.Message.ShouldNotContain("any algorithm is accepted");
            finding.Recommendation.ShouldNotBeNull().ShouldContain("mTLS");
            finding.Recommendation.ShouldContain(FS10.RecommendedOption);
        }

        [Theory]
        [MemberData(nameof(AlgorithmRules))]
        public void Signing_algorithm_rule_only_assesses_its_own_algorithms(AlgorithmRule rule)
        {
            var finding = AssessAlgorithmRule(rule, ["PS256"], otherAlgorithms: ["RS256"]);

            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Theory]
        [InlineData(60)]
        [InlineData(300)]
        [InlineData(599)]
        public void FS04_PAR_lifetime_within_range_passes(int lifetime)
        {
            var options = CreateDefaultServerOptions(parLifetime: lifetime);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS04");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Theory]
        [InlineData(600)]
        [InlineData(900)]
        public void FS04_PAR_lifetime_at_or_above_limit_fails(int lifetime)
        {
            var options = CreateDefaultServerOptions(parLifetime: lifetime);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS04");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Recommendation!.ShouldContain("600");
        }

        [Fact]
        public void FS06_issuer_identification_enabled_passes()
        {
            var options = CreateDefaultServerOptions(emitIssuer: true);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS06");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("enabled");
        }

        [Fact]
        public void FS06_issuer_identification_disabled_fails()
        {
            var options = CreateDefaultServerOptions(emitIssuer: false);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS06");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("mix-up attack");
            _ = finding.Recommendation.ShouldNotBeNull();
        }

        [Fact]
        public void FS05_mtls_and_dpop_available_passes()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions(mtlsEnabled: true));

            var finding = GetFinding(assessor.AssessServer(), "FS05");

            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("mTLS and DPoP");
            finding.Recommendation.ShouldBeNull();
        }

        [Fact]
        public void FS05_only_dpop_available_passes()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions(mtlsEnabled: false));

            var finding = GetFinding(assessor.AssessServer(), "FS05");

            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("via DPoP");
            finding.Message.ShouldNotContain("mTLS and");
        }

        [Fact]
        public void FS05_only_mtls_available_passes()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions(mtlsEnabled: true, dpopSigningAlgorithms: []));

            var finding = GetFinding(assessor.AssessServer(), "FS05");

            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("via mTLS.");
        }

        [Fact]
        public void FS05_no_mechanism_available_fails()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions(mtlsEnabled: false, dpopSigningAlgorithms: []));

            var finding = GetFinding(assessor.AssessServer(), "FS05");

            finding.Status.ShouldBe(FindingStatus.Fail);
            _ = finding.Recommendation.ShouldNotBeNull();
        }

        [Fact]
        public void FS07_http_303_redirects_always_passes()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions());

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS07");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.RuleName.ShouldBe("HTTP 303 Redirects");
            finding.Message.ShouldContain("303");
        }

        [Fact]
        public void FS08_pkce_support_always_passes()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions());

            var finding = GetFinding(assessor.AssessServer(), "FS08");

            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.RuleName.ShouldBe("PKCE Support");
            finding.Message.ShouldContain("S256");
        }

        [Fact]
        public void FS11_discovery_endpoint_enabled_passes()
        {
            var options = CreateDefaultServerOptions(discoveryEnabled: true);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS11");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Recommendation.ShouldBeNull();
        }

        [Fact]
        public void FS11_discovery_endpoint_disabled_fails()
        {
            var options = CreateDefaultServerOptions(discoveryEnabled: false);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS11");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Recommendation.ShouldBe("Set Endpoints.EnableDiscoveryEndpoint = true.");
        }

        [Fact]
        public void FS12_issuer_uri_not_set_passes()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions());

            var finding = GetFinding(assessor.AssessServer(), "FS12");

            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FS12_https_issuer_uri_passes()
        {
            var options = CreateDefaultServerOptions() with { IssuerUri = "https://idp.example.com" };
            var assessor = new Fapi2SecurityAssessor(options);

            var finding = GetFinding(assessor.AssessServer(), "FS12");

            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Recommendation.ShouldBeNull();
        }

        [Theory]
        [InlineData("http://idp.example.com")]
        [InlineData("idp.example.com")]
        public void FS12_non_https_issuer_uri_fails(string issuerUri)
        {
            var options = CreateDefaultServerOptions() with { IssuerUri = issuerUri };
            var assessor = new Fapi2SecurityAssessor(options);

            var finding = GetFinding(assessor.AssessServer(), "FS12");

            finding.Status.ShouldBe(FindingStatus.Fail);
            _ = finding.Recommendation.ShouldNotBeNull();
        }
    }

    public class ClientAssessments
    {
        private readonly Fapi2SecurityAssessor _assessor = new(CreateDefaultServerOptions());

        [Theory]
        [InlineData("AuthorizationCode", FindingStatus.Pass)]
        [InlineData("ClientCredentials", FindingStatus.Pass)]
        [InlineData("RefreshToken", FindingStatus.Pass)]
        [InlineData("DeviceCode", FindingStatus.Pass)]
        [InlineData("Ciba", FindingStatus.Pass)]
        [InlineData("Implicit", FindingStatus.Fail)]
        [InlineData("Hybrid", FindingStatus.Fail)]
        [InlineData("Password", FindingStatus.Fail)]
        public void FC01_grant_type_validation(string grantType, FindingStatus expectedStatus)
        {
            var grantTypes = grantType switch
            {
                "AuthorizationCode" => new[] { ConformanceReportGrantTypes.AuthorizationCode },
                "ClientCredentials" => new[] { ConformanceReportGrantTypes.ClientCredentials },
                "RefreshToken" => new[] { ConformanceReportGrantTypes.RefreshToken },
                "DeviceCode" => new[] { ConformanceReportGrantTypes.DeviceCode },
                "Ciba" => new[] { "urn:openid:params:grant-type:ciba" },
                "Implicit" => new[] { ConformanceReportGrantTypes.Implicit },
                "Hybrid" => new[] { ConformanceReportGrantTypes.Hybrid },
                "Password" => new[] { ConformanceReportGrantTypes.Password },
                _ => throw new ArgumentException($"Unknown grant type: {grantType}")
            };

            var client = CreateFapi2CompliantClient(grantTypes: grantTypes);
            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC01");
            finding.Status.ShouldBe(expectedStatus);
            if (expectedStatus == FindingStatus.Fail && grantType == "Implicit")
            {
                finding.Message.ShouldContain("implicit");
            }
        }

        [Fact]
        public void FC02_confidential_client_passes()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requireClientSecret: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC02");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FC02_public_client_fails()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requireClientSecret: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC02");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("public");
        }

        [Fact]
        public void FC02_public_client_credentials_client_fails()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.ClientCredentials],
                requireClientSecret: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC02");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("public");
            finding.Message.ShouldNotContain("authorization_code");
        }

        [Theory]
        [InlineData("FC03")]
        [InlineData("FC07")]
        [InlineData("FC10")]
        [InlineData("FC13")]
        public void rule_not_applicable_for_client_credentials(string ruleId)
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.ClientCredentials]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, ruleId);
            finding.Status.ShouldBe(FindingStatus.NotApplicable);
        }

        [Fact]
        public void FC03_PKCE_S256_passes()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePkce: true,
                allowPlainTextPkce: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC03");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FC03_PKCE_not_required_fails()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePkce: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC03");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("PKCE is not required");
        }

        [Fact]
        public void FC03_plain_text_PKCE_fails()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePkce: true,
                allowPlainTextPkce: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC03");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("Plain text PKCE");
        }



        [Fact]
        public void FC04_PAR_required_client_passes()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePar: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC04");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FC04_PAR_not_required_fails()
        {
            var options = CreateDefaultServerOptions(parRequired: false);
            var assessor = new Fapi2SecurityAssessor(options);
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePar: false);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC04");
            finding.Status.ShouldBe(FindingStatus.Fail);
        }

        [Fact]
        public void FC04_PAR_required_server_wide_passes()
        {
            var options = CreateDefaultServerOptions(parRequired: true);
            var assessor = new Fapi2SecurityAssessor(options);
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePar: false);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC04");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FC05_DPoP_required_passes()
        {
            var client = CreateFapi2CompliantClient(requireDPoP: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC05");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("DPoP");
        }

        [Fact]
        public void FC05_MTLS_passes()
        {
            var client = CreateFapi2CompliantClient(
                requireDPoP: false,
                secretTypes: [ConformanceReportSecretTypes.X509CertificateThumbprint]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC05");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("mTLS");
        }

        [Fact]
        public void FC05_no_sender_constraint_fails()
        {
            var client = CreateFapi2CompliantClient(
                requireDPoP: false,
                secretTypes: [ConformanceReportSecretTypes.SharedSecret]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC05");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("FAPI 2.0 requires");
        }

        [Fact]
        public void FC05_MTLS_with_X509_name_passes()
        {
            var client = CreateFapi2CompliantClient(
                requireDPoP: false,
                secretTypes: [ConformanceReportSecretTypes.X509CertificateName]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC05");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("mTLS");
        }

        [Fact]
        public void FC05_MTLS_with_thumbprint_and_name_passes()
        {
            var client = CreateFapi2CompliantClient(
                requireDPoP: false,
                secretTypes:
                [
                    ConformanceReportSecretTypes.X509CertificateThumbprint,
                    ConformanceReportSecretTypes.X509CertificateName
                ]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC05");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("mTLS");
        }

        public static TheoryData<string[]> NonMtlsSecretTypeSets => new(
            [],
            [ConformanceReportSecretTypes.X509CertificateThumbprint, ConformanceReportSecretTypes.JsonWebKey],
            [ConformanceReportSecretTypes.X509CertificateThumbprint, ConformanceReportSecretTypes.SharedSecret],
            [ConformanceReportSecretTypes.X509CertificateBase64]);

        [Theory]
        [MemberData(nameof(NonMtlsSecretTypeSets))]
        public void FC05_without_DPoP_and_secrets_not_all_mTLS_fails(string[] secretTypes)
        {
            var client = CreateFapi2CompliantClient(
                requireDPoP: false,
                secretTypes: secretTypes);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC05");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("FAPI 2.0 requires");
        }

        [Fact]
        public void FC05_public_client_with_thumbprint_fails()
        {
            var client = CreateFapi2CompliantClient(
                requireDPoP: false,
                requireClientSecret: false,
                secretTypes: [ConformanceReportSecretTypes.X509CertificateThumbprint]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC05");
            finding.Status.ShouldBe(FindingStatus.Fail);
        }

        [Fact]
        public void FC05_thumbprint_with_MTLS_disabled_fails()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions(mtlsEnabled: false));
            var client = CreateFapi2CompliantClient(
                requireDPoP: false,
                secretTypes: [ConformanceReportSecretTypes.X509CertificateThumbprint]);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC05");
            finding.Status.ShouldBe(FindingStatus.Fail);
        }

        [Fact]
        public void FC05_DPoP_required_but_DPoP_disabled_with_mTLS_enabled_and_JWK_secret_fails()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions(mtlsEnabled: true, dpopSigningAlgorithms: []));
            var client = CreateFapi2CompliantClient(
                requireDPoP: true,
                secretTypes: [ConformanceReportSecretTypes.JsonWebKey]);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC05");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("no DPoP proof algorithms are configured");
            finding.Recommendation.ShouldNotBeNull().ShouldContain("SupportedDPoPSigningAlgorithms");
        }

        [Fact]
        public void FC05_DPoP_required_but_DPoP_disabled_with_mTLS_bound_passes_via_mTLS()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions(mtlsEnabled: true, dpopSigningAlgorithms: []));
            var client = CreateFapi2CompliantClient(
                requireDPoP: true,
                secretTypes: [ConformanceReportSecretTypes.X509CertificateThumbprint]);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC05");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("via mTLS");
            finding.Message.ShouldNotContain("DPoP");
        }

        [Fact]
        public void FC05_DPoP_with_MTLS_disabled_passes()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions(mtlsEnabled: false));
            var client = CreateFapi2CompliantClient(
                requireDPoP: true,
                secretTypes: [ConformanceReportSecretTypes.JsonWebKey]);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC05");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("DPoP");
            finding.Message.ShouldNotContain("mTLS");
        }

        [Fact]
        public void FC05_DPoP_and_MTLS_both_bound_passes()
        {
            var client = CreateFapi2CompliantClient(
                requireDPoP: true,
                secretTypes: [ConformanceReportSecretTypes.X509CertificateThumbprint]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC05");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("DPoP and mTLS");
        }

        [Fact]
        public void FC05_DPoP_with_mixed_secrets_passes_via_DPoP_only()
        {
            var client = CreateFapi2CompliantClient(
                requireDPoP: true,
                secretTypes:
                [
                    ConformanceReportSecretTypes.X509CertificateThumbprint,
                    ConformanceReportSecretTypes.SharedSecret
                ]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC05");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("DPoP");
            finding.Message.ShouldNotContain("mTLS");
        }

        [Fact]
        public void FC06_private_key_JWT_passes()
        {
            var client = CreateFapi2CompliantClient(
                requireClientSecret: true,
                secretTypes: [ConformanceReportSecretTypes.JsonWebKey]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC06");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("private_key_jwt");
        }

        [Fact]
        public void FC06_MTLS_passes()
        {
            var client = CreateFapi2CompliantClient(
                requireClientSecret: true,
                secretTypes: [ConformanceReportSecretTypes.X509CertificateThumbprint]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC06");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("mTLS");
        }

        [Fact]
        public void FC06_shared_secret_fails()
        {
            var client = CreateFapi2CompliantClient(
                requireClientSecret: true,
                secretTypes: [ConformanceReportSecretTypes.SharedSecret]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC06");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain(ConformanceReportSecretTypes.SharedSecret);
        }

        [Theory]
        [InlineData(ConformanceReportSecretTypes.JsonWebKey)]
        [InlineData(ConformanceReportSecretTypes.X509CertificateThumbprint)]
        public void FC06_shared_secret_alongside_secure_method_fails(string secureSecretType)
        {
            var client = CreateFapi2CompliantClient(
                requireClientSecret: true,
                secretTypes: [secureSecretType, ConformanceReportSecretTypes.SharedSecret]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC06");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain(ConformanceReportSecretTypes.SharedSecret);
        }

        [Fact]
        public void FC06_unrecognized_secret_type_alongside_secure_method_fails()
        {
            var client = CreateFapi2CompliantClient(
                requireClientSecret: true,
                secretTypes: [ConformanceReportSecretTypes.JsonWebKey, "CustomSecretType"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC06");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("CustomSecretType");
        }

        [Fact]
        public void FC06_private_key_JWT_and_MTLS_together_passes()
        {
            var client = CreateFapi2CompliantClient(
                requireClientSecret: true,
                secretTypes:
                [
                    ConformanceReportSecretTypes.X509CertificateBase64,
                    ConformanceReportSecretTypes.X509CertificateName
                ]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC06");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("private_key_jwt");
            finding.Message.ShouldContain("mTLS");
        }

        [Fact]
        public void FC06_no_secure_secret_fails()
        {
            var client = CreateFapi2CompliantClient(
                requireClientSecret: true,
                secretTypes: []);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC06");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("no private_key_jwt or mTLS");
        }

        [Theory]
        [InlineData(30)]
        [InlineData(60)]
        public void FC07_auth_code_lifetime_within_range_passes(int seconds)
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                authCodeLifetime: seconds);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC07");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Theory]
        [InlineData(61)]
        [InlineData(120)]
        public void FC07_auth_code_lifetime_exceeds_range_fails(int seconds)
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                authCodeLifetime: seconds);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC07");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Recommendation!.ShouldContain("60");
        }



        [Fact]
        public void FC08_refresh_token_reuse_passes()
        {
            var client = CreateFapi2CompliantClient(
                allowOfflineAccess: true,
                refreshTokenUsage: ConformanceReportTokenUsage.ReUse);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC08");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("reusable");
            finding.Recommendation.ShouldBeNull();
        }

        [Fact]
        public void FC08_refresh_token_rotation_warns()
        {
            var client = CreateFapi2CompliantClient(
                allowOfflineAccess: true,
                refreshTokenUsage: ConformanceReportTokenUsage.OneTimeOnly);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC08");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Message.ShouldContain("discourages");
            finding.Recommendation.ShouldBe("Set RefreshTokenUsage = TokenUsage.ReUse.");
        }

        [Fact]
        public void FC08_not_applicable_no_offline_access()
        {
            var client = CreateFapi2CompliantClient(allowOfflineAccess: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC08");
            finding.Status.ShouldBe(FindingStatus.NotApplicable);
        }

        [Fact]
        public void FC09_dpop_nonce_enabled_passes()
        {
            var client = CreateFapi2CompliantClient(requireDPoP: true, dpopMode: ConformanceReportDPoPValidationMode.Nonce);

            var finding = GetFinding(_assessor.AssessClient(client), "FC09");

            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("is enabled");
        }

        [Fact]
        public void FC09_dpop_nonce_disabled_still_passes()
        {
            var client = CreateFapi2CompliantClient(requireDPoP: true, dpopMode: ConformanceReportDPoPValidationMode.Iat);

            var finding = GetFinding(_assessor.AssessClient(client), "FC09");

            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("not enabled");
            finding.Recommendation.ShouldBeNull();
        }

        [Fact]
        public void FC09_not_applicable_without_dpop()
        {
            var client = CreateFapi2CompliantClient(requireDPoP: false);

            var finding = GetFinding(_assessor.AssessClient(client), "FC09");

            finding.Status.ShouldBe(FindingStatus.NotApplicable);
        }

        [Fact]
        public void FC09_not_applicable_when_dpop_algorithms_empty()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions(dpopSigningAlgorithms: []));
            var client = CreateFapi2CompliantClient(requireDPoP: true);

            var finding = GetFinding(assessor.AssessClient(client), "FC09");

            finding.Status.ShouldBe(FindingStatus.NotApplicable);
        }

        [Theory]
        [InlineData("https://*.example.com/callback")]
        [InlineData("https://app.*.example.com")]
        public void FC10_wildcard_host_redirect_uri_fails_with_wildcard_message(string redirectUri)
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["https://example.com/callback", redirectUri]);

            var finding = GetFinding(_assessor.AssessClient(client), "FC10");

            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("wildcard");
            finding.Message.ShouldContain(redirectUri);
            finding.Message.ShouldNotContain("https://example.com/callback");
            _ = finding.Recommendation.ShouldNotBeNull();
        }

        [Theory]
        [InlineData("https://example.com/callback")]
        [InlineData("https://example.com/*")]
        [InlineData("https://example.com/callback?x=*")]
        public void FC10_redirect_uri_without_wildcard_host_passes(string redirectUri)
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: [redirectUri]);

            var finding = GetFinding(_assessor.AssessClient(client), "FC10");

            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Recommendation.ShouldBeNull();
        }

        [Fact]
        public void FC10_not_applicable_when_no_redirect_uris_registered()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: []);

            var finding = GetFinding(_assessor.AssessClient(client), "FC10");

            finding.Status.ShouldBe(FindingStatus.NotApplicable);
        }

        [Fact]
        public void FC13_https_redirect_uri_passes()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["https://example.com/callback"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC13");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FC13_no_redirect_uris_fails()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: []);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC13");
            finding.Status.ShouldBe(FindingStatus.Fail);
        }

        [Fact]
        public void FC13_no_redirect_uris_passes_when_unregistered_pushed_redirect_uris_allowed()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions(allowUnregisteredPushedRedirectUris: true));
            var client = CreateFapi2CompliantClient(redirectUris: []);

            var finding = GetFinding(assessor.AssessClient(client), "FC13");

            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FC13_no_redirect_uris_fails_when_par_endpoint_disabled_even_when_unregistered_pushed_redirect_uris_allowed()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions(parEnabled: false, allowUnregisteredPushedRedirectUris: true));
            var client = CreateFapi2CompliantClient(redirectUris: []);

            var finding = GetFinding(assessor.AssessClient(client), "FC13");

            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("PAR endpoint is disabled");
        }

        [Fact]
        public void FC13_no_redirect_uris_fails_for_public_client_even_when_unregistered_pushed_redirect_uris_allowed()
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions(allowUnregisteredPushedRedirectUris: true));
            var client = CreateFapi2CompliantClient(redirectUris: [], requireClientSecret: false);

            var finding = GetFinding(assessor.AssessClient(client), "FC13");

            finding.Status.ShouldBe(FindingStatus.Fail);
        }

        [Theory]
        [InlineData("http://example.com/callback")]
        [InlineData("http://app.example.com:8080/callback")]
        public void FC13_http_redirect_uri_fails(string redirectUri)
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["https://example.com/callback", redirectUri]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC13");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain(redirectUri);
            finding.Message.ShouldContain("5.3.1.2");
            finding.Message.ShouldNotContain("Malformed");
        }

        [Theory]
        [InlineData("not a valid uri")]
        public void FC13_malformed_redirect_uri_fails(string redirectUri)
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["https://example.com/callback", redirectUri]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC13");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("Malformed");
            finding.Message.ShouldContain(redirectUri);
            finding.Message.ShouldNotContain("5.3.1.2");
        }

        [Fact]
        public void FC13_malformed_and_http_redirect_uris_are_reported_separately()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["not a valid uri", "http://example.com/callback"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC13");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("Malformed redirect URIs (not absolute URIs): not a valid uri.");
            finding.Message.ShouldContain("Insecure redirect URIs detected: http://example.com/callback.");
        }

        [Theory]
        [InlineData("http://127.0.0.1/callback")]
        [InlineData("http://127.0.0.1:52341/callback")]
        [InlineData("http://[::1]:8080/callback")]
        public void FC13_http_loopback_redirect_uri_passes_when_loopback_enabled(string redirectUri)
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions(loopbackRedirectUrisEnabled: true));
            var client = CreateFapi2CompliantClient(redirectUris: [redirectUri]);

            var finding = GetFinding(assessor.AssessClient(client), "FC13");

            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Theory]
        [InlineData("http://127.0.0.1:52341/callback")]
        [InlineData("http://[::1]:8080/callback")]
        [InlineData("http://localhost/callback")]
        public void FC13_http_loopback_redirect_uri_fails_when_loopback_not_enabled(string redirectUri)
        {
            var client = CreateFapi2CompliantClient(redirectUris: [redirectUri]);

            var finding = GetFinding(_assessor.AssessClient(client), "FC13");

            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain(redirectUri);
            finding.Message.ShouldContain("not configured for loopback redirection");
        }

        [Theory]
        [InlineData("http://localhost/callback")]
        [InlineData("http://LOCALHOST:3000/callback")]
        public void FC13_http_localhost_redirect_uri_warns_when_loopback_enabled(string redirectUri)
        {
            var assessor = new Fapi2SecurityAssessor(CreateDefaultServerOptions(loopbackRedirectUrisEnabled: true));
            var client = CreateFapi2CompliantClient(redirectUris: [redirectUri]);

            var finding = GetFinding(assessor.AssessClient(client), "FC13");

            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Message.ShouldContain(redirectUri);
            finding.Message.ShouldContain("8.3");
        }

        [Fact]
        public void FC13_custom_scheme_redirect_uri_passes()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["com.example.app:/callback"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC13");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FC13_wildcard_in_https_redirect_uri_is_not_rejected()
        {
            // FC13 only checks the scheme. A '*' outside the host is matched literally.
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["https://example.com/*"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC13");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FC13_wildcard_host_redirect_uri_is_left_to_FC10()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["https://example.com/callback", "https://*.example.com/callback"]);

            var finding = GetFinding(_assessor.AssessClient(client), "FC13");

            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldNotContain("Malformed");
        }

        [Fact]
        public void FC13_not_applicable_when_all_redirect_uris_have_wildcard_hosts()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["https://*.example.com/callback"]);

            var finding = GetFinding(_assessor.AssessClient(client), "FC13");

            finding.Status.ShouldBe(FindingStatus.NotApplicable);
            finding.Message.ShouldContain("FC10");
        }
    }

    public class CompleteConfigurationTests
    {
        [Fact]
        public void FAPI2_compliant_server_has_all_passes()
        {
            var options = CreateDefaultServerOptions(
                parEnabled: true,
                parRequired: true,
                parLifetime: 599,
                mtlsEnabled: true,
                signingAlgorithms: ["PS256", "ES256"],
                emitIssuer: true);

            var assessor = new Fapi2SecurityAssessor(options);
            var findings = assessor.AssessServer();

            findings.ShouldNotBeEmpty();
            findings.ShouldAllHaveStatus(FindingStatus.Pass);
        }

        [Fact]
        public void FAPI2_compliant_client_has_all_passes()
        {
            var options = CreateDefaultServerOptions();
            var assessor = new Fapi2SecurityAssessor(options);

            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode, ConformanceReportGrantTypes.RefreshToken],
                requirePkce: true,
                allowPlainTextPkce: false,
                redirectUris: ["https://example.com/callback"],
                requireClientSecret: true,
                secretTypes: [ConformanceReportSecretTypes.JsonWebKey],
                requirePar: true,
                requireDPoP: true,
                dpopMode: ConformanceReportDPoPValidationMode.None,
                authCodeLifetime: 60,
                allowOfflineAccess: true,
                refreshTokenUsage: ConformanceReportTokenUsage.ReUse,
                allowAccessTokensViaBrowser: false,
                requireRequestObject: false);

            var findings = assessor.AssessClient(client);

            findings.ShouldNotBeEmpty();
            findings.ShouldAllHaveStatus(FindingStatus.Pass, FindingStatus.NotApplicable);
        }

        [Fact]
        public void non_compliant_server_with_RS256_has_failure()
        {
            var options = CreateDefaultServerOptions(signingAlgorithms: ["RS256"]);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS03");
            finding.Status.ShouldBe(FindingStatus.Fail);
        }

        [Fact]
        public void non_compliant_client_with_shared_secret_has_failures()
        {
            var options = CreateDefaultServerOptions();
            var assessor = new Fapi2SecurityAssessor(options);

            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requireClientSecret: true,
                secretTypes: [ConformanceReportSecretTypes.SharedSecret],
                requireDPoP: false);

            var findings = assessor.AssessClient(client);

            // Should fail on FC05 (sender-constrained) and FC06 (client auth)
            var fc05 = GetFinding(findings, "FC05");
            var fc06 = GetFinding(findings, "FC06");

            fc05.Status.ShouldBe(FindingStatus.Fail);
            fc06.Status.ShouldBe(FindingStatus.Fail);
        }
    }
}
