// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.ConformanceReport.Models;

namespace Duende.ConformanceReport.Services;

public class Fapi2SecurityAssessorTests
{
    private static ConformanceReportServerOptions CreateDefaultServerOptions(
        bool parEnabled = true,
        bool parRequired = true,
        int parLifetime = 600,
        bool mtlsEnabled = true,
        IReadOnlyCollection<string>? signingAlgorithms = null,
        TimeSpan? clockSkew = null,
        bool emitIssuer = true,
        bool useHttp303Redirects = true) =>
        new()
        {
            PushedAuthorizationEndpointEnabled = parEnabled,
            PushedAuthorizationRequired = parRequired,
            PushedAuthorizationLifetime = parLifetime,
            MutualTlsEnabled = mtlsEnabled,
            SupportedSigningAlgorithms = signingAlgorithms ?? ["PS256", "ES256"],
            JwtValidationClockSkew = clockSkew ?? TimeSpan.FromMinutes(5),
            EmitIssuerIdentificationResponseParameter = emitIssuer,
            UseHttp303Redirects = useHttp303Redirects
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
        public void FS01PAREnabledAndRequiredPasses()
        {
            var options = CreateDefaultServerOptions(parEnabled: true, parRequired: true);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS01");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("required globally");
        }

        [Fact]
        public void FS01PAREnabledNotRequiredWarns()
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
        public void FS01PARDisabledFails()
        {
            var options = CreateDefaultServerOptions(parEnabled: false, parRequired: false);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS01");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("not enabled");
        }

        [Fact]
        public void FS02MTLSEnabledPasses()
        {
            var options = CreateDefaultServerOptions(mtlsEnabled: true);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS02");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("mTLS is enabled");
        }

        [Fact]
        public void FS02MTLSDisabledWarns()
        {
            var options = CreateDefaultServerOptions(mtlsEnabled: false);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS02");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Message.ShouldContain("mTLS is not enabled");
        }

        [Theory]
        [InlineData("PS256")]
        [InlineData("ES256")]
        [InlineData("PS256,ES256")]
        [InlineData("PS384,PS512")]
        [InlineData("ES384,ES512")]
        [InlineData("PS256,PS384,PS512,ES256,ES384,ES512")]
        public void FS03FAPICompliantAlgorithmsPasses(string algorithmsCommaSeparated)
        {
            var algorithms = algorithmsCommaSeparated.Split(',');
            var options = CreateDefaultServerOptions(signingAlgorithms: algorithms);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS03");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FS03RS256MixedWithFAPIWarns()
        {
            var options = CreateDefaultServerOptions(signingAlgorithms: ["PS256", "RS256"]);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS03");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Message.ShouldContain("RS256");
        }

        [Fact]
        public void FS03OnlyNonFAPIAlgorithmsFails()
        {
            var options = CreateDefaultServerOptions(signingAlgorithms: ["RS256", "HS256"]);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS03");
            finding.Status.ShouldBe(FindingStatus.Fail);
        }

        [Theory]
        [InlineData(60)]
        [InlineData(300)]
        [InlineData(600)]
        public void FS04PARLifetimeWithinRangePasses(int lifetime)
        {
            var options = CreateDefaultServerOptions(parLifetime: lifetime);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS04");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Theory]
        [InlineData(601)]
        [InlineData(900)]
        public void FS04PARLifetimeExceedsRangeFails(int lifetime)
        {
            var options = CreateDefaultServerOptions(parLifetime: lifetime);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS04");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Recommendation!.ShouldContain("600");
        }

        [Fact]
        public void FS05MTLSEnabledPasses()
        {
            var options = CreateDefaultServerOptions(mtlsEnabled: true);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS05");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("mTLS is enabled");
        }

        [Fact]
        public void FS05MTLSDisabledStillPassesDPoPAvailable()
        {
            var options = CreateDefaultServerOptions(mtlsEnabled: false);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS05");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("DPoP is available");
        }

        [Fact]
        public void FS06IssuerIdentificationEnabledPasses()
        {
            var options = CreateDefaultServerOptions(emitIssuer: true);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS06");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("enabled");
        }

        [Fact]
        public void FS06IssuerIdentificationDisabledFails()
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
        public void FS07Http303RedirectsEnabledPasses()
        {
            var options = CreateDefaultServerOptions(useHttp303Redirects: true);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS07");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("303");
        }

        [Fact]
        public void FS07Http303RedirectsDisabledFails()
        {
            var options = CreateDefaultServerOptions(useHttp303Redirects: false);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS07");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("Section 5.3.2.2");
        }

        [Fact]
        public void FS08PKCESupportPasses()
        {
            var options = CreateDefaultServerOptions();
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS08");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }
    }

    public class ClientAssessments
    {
        private readonly Fapi2SecurityAssessor _assessor = new(CreateDefaultServerOptions());

        [Theory]
        [InlineData("AuthorizationCode", FindingStatus.Pass)]
        [InlineData("ClientCredentials", FindingStatus.Pass)]
        [InlineData("Implicit", FindingStatus.Fail)]
        [InlineData("Password", FindingStatus.Fail)]
        [InlineData("DeviceCode", FindingStatus.Fail)]
        public void FC01GrantTypeValidation(string grantType, FindingStatus expectedStatus)
        {
            var grantTypes = grantType switch
            {
                "AuthorizationCode" => new[] { ConformanceReportGrantTypes.AuthorizationCode },
                "ClientCredentials" => new[] { ConformanceReportGrantTypes.ClientCredentials },
                "Implicit" => new[] { ConformanceReportGrantTypes.Implicit },
                "Password" => new[] { ConformanceReportGrantTypes.Password },
                "DeviceCode" => new[] { ConformanceReportGrantTypes.DeviceCode },
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
        public void FC02ConfidentialClientPasses()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requireClientSecret: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC02");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FC02PublicClientFails()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requireClientSecret: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC02");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("public");
        }

        [Theory]
        [InlineData("FC02")]
        [InlineData("FC03")]
        [InlineData("FC07")]
        [InlineData("FC10")]
        [InlineData("FC11")]
        [InlineData("FC12")]
        public void RuleNotApplicableForClientCredentials(string ruleId)
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.ClientCredentials]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, ruleId);
            finding.Status.ShouldBe(FindingStatus.NotApplicable);
        }

        [Fact]
        public void FC03PKCES256Passes()
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
        public void FC03PKCENotRequiredFails()
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
        public void FC03PlainTextPKCEFails()
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
        public void FC04PARRequiredClientPasses()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePar: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC04");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FC04PARNotRequiredFails()
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
        public void FC04PARRequiredServerWidePasses()
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
        public void FC05DPoPRequiredPasses()
        {
            var client = CreateFapi2CompliantClient(requireDPoP: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC05");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("DPoP");
        }

        [Fact]
        public void FC05MTLSPasses()
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
        public void FC05NoSenderConstraintFails()
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
        public void FC06PrivateKeyJWTPasses()
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
        public void FC06MTLSPasses()
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
        public void FC06SharedSecretFails()
        {
            var client = CreateFapi2CompliantClient(
                requireClientSecret: true,
                secretTypes: [ConformanceReportSecretTypes.SharedSecret]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC06");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("shared secret");
        }

        [Fact]
        public void FC06PublicClientFails()
        {
            var client = CreateFapi2CompliantClient(requireClientSecret: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC06");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("public");
        }

        [Theory]
        [InlineData(30)]
        [InlineData(60)]
        public void FC07AuthCodeLifetimeWithinRangePasses(int seconds)
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
        public void FC07AuthCodeLifetimeExceedsRangeFails(int seconds)
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
        public void FC08RefreshTokenRotationEnabledPasses()
        {
            var client = CreateFapi2CompliantClient(
                allowOfflineAccess: true,
                refreshTokenUsage: ConformanceReportTokenUsage.OneTimeOnly);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC08");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FC08RefreshTokenRotationDisabledFails()
        {
            var client = CreateFapi2CompliantClient(
                allowOfflineAccess: true,
                refreshTokenUsage: ConformanceReportTokenUsage.ReUse);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC08");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("reusable");
        }

        [Fact]
        public void FC08NotApplicableNoOfflineAccess()
        {
            var client = CreateFapi2CompliantClient(allowOfflineAccess: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC08");
            finding.Status.ShouldBe(FindingStatus.NotApplicable);
        }

        [Fact]
        public void FC09DPoPNonceEnabledPasses()
        {
            var client = CreateFapi2CompliantClient(
                requireDPoP: true,
                dpopMode: ConformanceReportDPoPValidationMode.Nonce);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC09");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FC09DPoPNonceDisabledFails()
        {
            var client = CreateFapi2CompliantClient(
                requireDPoP: true,
                dpopMode: ConformanceReportDPoPValidationMode.None);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC09");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("replay protection");
        }

        [Fact]
        public void FC09DPoPNonceWithIatPasses()
        {
            var client = CreateFapi2CompliantClient(
                requireDPoP: true,
                dpopMode: ConformanceReportDPoPValidationMode.Nonce | ConformanceReportDPoPValidationMode.Iat);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC09");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FC09NotApplicableNoDPoP()
        {
            var client = CreateFapi2CompliantClient(requireDPoP: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC09");
            finding.Status.ShouldBe(FindingStatus.NotApplicable);
        }

        [Fact]
        public void FC10ExplicitRedirectUriPasses()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["https://example.com/callback"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC10");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FC10NoRedirectUrisFails()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: []);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC10");
            finding.Status.ShouldBe(FindingStatus.Fail);
        }

        [Fact]
        public void FC10WildcardRedirectUriFails()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["https://*.example.com/callback"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC10");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("Wildcard");
        }



        [Fact]
        public void FC11AccessTokensViaBrowserDisabledPasses()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                allowAccessTokensViaBrowser: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC11");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FC11AccessTokensViaBrowserEnabledFails()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                allowAccessTokensViaBrowser: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC11");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("prohibits");
        }



        [Fact]
        public void FC12RequestObjectRequiredPasses()
        {
            var options = CreateDefaultServerOptions(parRequired: false);
            var assessor = new Fapi2SecurityAssessor(options);
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePar: false,
                requireRequestObject: true);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC12");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void FC12PARRequiredPasses()
        {
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePar: true,
                requireRequestObject: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC12");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("PAR is required");
        }

        [Fact]
        public void FC12NeitherRequestObjectNorPARWarns()
        {
            var options = CreateDefaultServerOptions(parRequired: false);
            var assessor = new Fapi2SecurityAssessor(options);
            var client = CreateFapi2CompliantClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePar: false,
                requireRequestObject: false);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "FC12");
            finding.Status.ShouldBe(FindingStatus.Warning);
        }


    }

    public class CompleteConfigurationTests
    {
        [Fact]
        public void FAPI2CompliantServerHasAllPasses()
        {
            var options = CreateDefaultServerOptions(
                parEnabled: true,
                parRequired: true,
                parLifetime: 600,
                mtlsEnabled: true,
                signingAlgorithms: ["PS256", "ES256"],
                emitIssuer: true,
                useHttp303Redirects: true);

            var assessor = new Fapi2SecurityAssessor(options);
            var findings = assessor.AssessServer();

            findings.ShouldNotBeEmpty();
            findings.Count.ShouldBe(8); // FS01-FS08
            findings.ShouldNotContain(f => f.Status == FindingStatus.Fail);
        }

        [Fact]
        public void FAPI2CompliantClientHasAllPasses()
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
                dpopMode: ConformanceReportDPoPValidationMode.Nonce,
                authCodeLifetime: 60,
                allowOfflineAccess: true,
                refreshTokenUsage: ConformanceReportTokenUsage.OneTimeOnly,
                allowAccessTokensViaBrowser: false,
                requireRequestObject: false);

            var findings = assessor.AssessClient(client);

            findings.ShouldNotBeEmpty();
            findings.Count.ShouldBe(12); // FC01-FC12
            findings.ShouldNotContain(f => f.Status == FindingStatus.Fail);
        }

        [Fact]
        public void NonCompliantServerWithRS256HasFailure()
        {
            var options = CreateDefaultServerOptions(signingAlgorithms: ["RS256"]);
            var assessor = new Fapi2SecurityAssessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "FS03");
            finding.Status.ShouldBe(FindingStatus.Fail);
        }

        [Fact]
        public void NonCompliantClientWithSharedSecretHasFailures()
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
