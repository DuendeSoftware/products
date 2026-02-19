// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.ConformanceReport.Models;

namespace Duende.ConformanceReport.Services;

public class OAuth21AssessorTests
{
    private static ConformanceReportServerOptions CreateDefaultServerOptions(
        bool parEnabled = true,
        bool parRequired = false,
        bool mtlsEnabled = false,
        IReadOnlyCollection<string>? signingAlgorithms = null,
        TimeSpan? clockSkew = null,
        bool useHttp303Redirects = true) =>
        new()
        {
            PushedAuthorizationEndpointEnabled = parEnabled,
            PushedAuthorizationRequired = parRequired,
            PushedAuthorizationLifetime = 600,
            MutualTlsEnabled = mtlsEnabled,
            SupportedSigningAlgorithms = signingAlgorithms ?? ["RS256", "ES256"],
            JwtValidationClockSkew = clockSkew ?? TimeSpan.FromMinutes(5),
            EmitIssuerIdentificationResponseParameter = true,
            UseHttp303Redirects = useHttp303Redirects
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
        public void S01PKCESupportAlwaysPasses()
        {
            // Server always supports PKCE; client config is assessed separately
            var options = CreateDefaultServerOptions();
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "S01");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.RuleName.ShouldBe("PKCE Support");
        }

        [Fact]
        public void S02PasswordGrantProhibitionAlwaysPasses()
        {
            // Password grant is assessed at client level, so server level always passes
            var options = CreateDefaultServerOptions();
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "S02");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.RuleName.ShouldBe("Resource Owner Password Grant Prohibition");
        }

        [Fact]
        public void S03PAREnabledPasses()
        {
            var options = CreateDefaultServerOptions(parEnabled: true);
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "S03");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("PAR endpoint is enabled");
        }

        [Fact]
        public void S03PARDisabledWarns()
        {
            var options = CreateDefaultServerOptions(parEnabled: false);
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "S03");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Message.ShouldContain("not enabled");
            _ = finding.Recommendation.ShouldNotBeNull();
        }

        [Fact]
        public void S04SenderConstrainedMTLSEnabledPasses()
        {
            var options = CreateDefaultServerOptions(mtlsEnabled: true);
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "S04");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("mTLS");
        }

        [Fact]
        public void S04SenderConstrainedDPoPAlwaysSupported()
        {
            // DPoP is always supported when configured per-client
            var options = CreateDefaultServerOptions(mtlsEnabled: false);
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "S04");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("DPoP");
        }

        [Fact]
        public void S05SecureSigningAlgorithmsPasses()
        {
            var options = CreateDefaultServerOptions(signingAlgorithms: ["RS256", "ES256", "PS256"]);
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "S05");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("secure");
        }

        [Theory]
        [InlineData("HS256")]
        [InlineData("HS384")]
        [InlineData("HS512")]
        [InlineData("none")]
        public void S05InsecureSigningAlgorithmsFails(string algorithm)
        {
            var options = CreateDefaultServerOptions(signingAlgorithms: ["RS256", algorithm]);
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "S05");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain(algorithm);
            _ = finding.Recommendation.ShouldNotBeNull();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        public void S06ClockSkewWithinRangePasses(int minutes)
        {
            var options = CreateDefaultServerOptions(clockSkew: TimeSpan.FromMinutes(minutes));
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "S06");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Theory]
        [InlineData(6)]
        [InlineData(10)]
        public void S06ClockSkewExceedsRangeWarns(int minutes)
        {
            var options = CreateDefaultServerOptions(clockSkew: TimeSpan.FromMinutes(minutes));
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "S06");
            finding.Status.ShouldBe(FindingStatus.Warning);
            _ = finding.Recommendation.ShouldNotBeNull();
        }

        [Fact]
        public void S07DPoPNonceSupportPasses()
        {
            // DPoP nonce is configured per-client, server just notes support
            var options = CreateDefaultServerOptions();
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "S07");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void S08Http303RedirectsEnabledPasses()
        {
            var options = CreateDefaultServerOptions(useHttp303Redirects: true);
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "S08");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("303");
        }

        [Fact]
        public void S08Http303RedirectsDisabledWarns()
        {
            var options = CreateDefaultServerOptions(useHttp303Redirects: false);
            var assessor = new OAuth21Assessor(options);

            var findings = assessor.AssessServer();

            var finding = GetFinding(findings, "S08");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Message.ShouldContain("302");
            _ = finding.Recommendation.ShouldNotBeNull();
        }
    }

    public class ClientAssessments
    {
        private readonly OAuth21Assessor _assessor = new(CreateDefaultServerOptions());

        [Theory]
        [InlineData("AuthorizationCode", FindingStatus.Pass)]
        [InlineData("ClientCredentials", FindingStatus.Pass)]
        [InlineData("RefreshToken", FindingStatus.Pass)]
        [InlineData("Implicit", FindingStatus.Fail)]
        [InlineData("Password", FindingStatus.Fail)]
        public void C01GrantTypeValidation(string grantType, FindingStatus expectedStatus)
        {
            var grantTypes = grantType switch
            {
                "AuthorizationCode" => new[] { ConformanceReportGrantTypes.AuthorizationCode },
                "ClientCredentials" => new[] { ConformanceReportGrantTypes.ClientCredentials },
                "RefreshToken" => new[] { ConformanceReportGrantTypes.AuthorizationCode, ConformanceReportGrantTypes.RefreshToken },
                "Implicit" => new[] { ConformanceReportGrantTypes.Implicit },
                "Password" => new[] { ConformanceReportGrantTypes.Password },
                _ => throw new ArgumentException($"Unknown grant type: {grantType}")
            };

            var client = CreateDefaultClient(grantTypes: grantTypes);
            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C01");
            finding.Status.ShouldBe(expectedStatus);

            if (expectedStatus == FindingStatus.Fail)
            {
                if (grantType == "Implicit")
                {
                    finding.Message.ShouldContain("implicit");
                    finding.Recommendation!.ShouldContain("Remove implicit");
                }
                else if (grantType == "Password")
                {
                    finding.Message.ShouldContain("password");
                }
            }
        }

        [Fact]
        public void C02PKCERequiredForAuthCodePasses()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePkce: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C02");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void C02PKCENotRequiredForAuthCodeFails()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePkce: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C02");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("OAuth 2.1 mandates PKCE");
            finding.Recommendation!.ShouldContain("RequirePkce = true");
        }

        [Theory]
        [InlineData("C02")]
        [InlineData("C03")]
        [InlineData("C04")]
        [InlineData("C06")]
        [InlineData("C08")]
        [InlineData("C12")]
        public void RuleNotApplicableForClientCredentials(string ruleId)
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.ClientCredentials]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, ruleId);
            finding.Status.ShouldBe(FindingStatus.NotApplicable);
        }

        [Fact]
        public void C03PlainTextPkceDisabledPasses()
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
        public void C03PlainTextPkceEnabledFails()
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
        public void C04ExplicitRedirectUriPasses()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["https://example.com/callback"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void C04MultipleExplicitRedirectUrisPasses()
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
        public void C04NoRedirectUrisFails()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: []);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("No redirect URIs");
        }

        [Fact]
        public void C04WildcardRedirectUriFails()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                redirectUris: ["https://*.example.com/callback"]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C04");
            finding.Status.ShouldBe(FindingStatus.Fail);
            finding.Message.ShouldContain("Wildcard");
            finding.Recommendation!.ShouldContain("explicit");
        }



        [Fact]
        public void C05ConfidentialClientWithSecretPasses()
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
        public void C05ConfidentialClientNoSecretsFails()
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
        public void C05PublicClientWithPkcePasses()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requireClientSecret: false,
                requirePkce: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C05");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("Public client using authorization_code with PKCE");
        }

        [Fact]
        public void C05PublicClientWithoutPkceWarns()
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
        public void C06PARRequiredPasses()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePar: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C06");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void C06PARNotRequiredWarns()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePar: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C06");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Recommendation!.ShouldContain("RequirePushedAuthorization = true");
        }



        [Fact]
        public void C06PARRequiredServerWidePasses()
        {
            var options = CreateDefaultServerOptions(parRequired: true);
            var assessor = new OAuth21Assessor(options);
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                requirePar: false);

            var findings = assessor.AssessClient(client);

            var finding = GetFinding(findings, "C06");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void C07DPoPRequiredPasses()
        {
            var client = CreateDefaultClient(requireDPoP: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C07");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("DPoP");
        }

        [Fact]
        public void C07MTLSCertificatePasses()
        {
            var client = CreateDefaultClient(
                secretTypes: [ConformanceReportSecretTypes.X509CertificateThumbprint]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C07");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("mTLS");
        }

        [Fact]
        public void C07DPoPAndMTLSPasses()
        {
            var client = CreateDefaultClient(
                requireDPoP: true,
                secretTypes: [ConformanceReportSecretTypes.X509CertificateThumbprint]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C07");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("DPoP");
            finding.Message.ShouldContain("mTLS");
        }

        [Fact]
        public void C07NoSenderConstraintWarns()
        {
            var client = CreateDefaultClient(
                requireDPoP: false,
                secretTypes: [ConformanceReportSecretTypes.SharedSecret]);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C07");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Recommendation!.ShouldContain("DPoP");
        }

        [Theory]
        [InlineData(30)]
        [InlineData(60)]
        public void C08AuthCodeLifetimeWithinRangePasses(int seconds)
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                authCodeLifetime: seconds);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C08");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Theory]
        [InlineData(120)]
        [InlineData(300)]
        public void C08AuthCodeLifetimeTooLongWarns(int seconds)
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
        public void C09RefreshTokenRotationEnabledPasses()
        {
            var client = CreateDefaultClient(
                allowOfflineAccess: true,
                refreshTokenUsage: ConformanceReportTokenUsage.OneTimeOnly);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C09");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("one-time use");
        }

        [Fact]
        public void C09RefreshTokenRotationDisabledWarns()
        {
            var client = CreateDefaultClient(
                allowOfflineAccess: true,
                refreshTokenUsage: ConformanceReportTokenUsage.ReUse);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C09");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Message.ShouldContain("reusable");
            finding.Recommendation!.ShouldContain("RefreshTokenUsage");
        }

        [Fact]
        public void C09RefreshTokenRotationNotApplicableNoOfflineAccess()
        {
            var client = CreateDefaultClient(allowOfflineAccess: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C09");
            finding.Status.ShouldBe(FindingStatus.NotApplicable);
        }

        [Fact]
        public void C10DPoPNonceEnabledPasses()
        {
            var client = CreateDefaultClient(
                requireDPoP: true,
                dpopMode: ConformanceReportDPoPValidationMode.Nonce);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C10");
            finding.Status.ShouldBe(FindingStatus.Pass);
            finding.Message.ShouldContain("nonce validation is enabled");
        }

        [Fact]
        public void C10DPoPNonceDisabledWarns()
        {
            var client = CreateDefaultClient(
                requireDPoP: true,
                dpopMode: ConformanceReportDPoPValidationMode.None);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C10");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Message.ShouldContain("not enabled");
        }

        [Fact]
        public void C10DPoPNotApplicableWhenNotRequired()
        {
            var client = CreateDefaultClient(requireDPoP: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C10");
            finding.Status.ShouldBe(FindingStatus.NotApplicable);
        }

        [Theory]
        [InlineData("JsonWebKey", "private_key_jwt")]
        [InlineData("X509CertificateThumbprint", "mTLS")]
        [InlineData("X509CertificateName", null)]
        public void C11SecureSecretTypesPasses(string secretType, string? expectedMessageSubstring)
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
        public void C11SharedSecretWarns()
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
        public void C11PublicClientNotApplicable()
        {
            var client = CreateDefaultClient(requireClientSecret: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C11");
            finding.Status.ShouldBe(FindingStatus.NotApplicable);
            finding.Message.ShouldContain("public client");
        }

        [Fact]
        public void C12RefreshTokensEnabledPasses()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                allowOfflineAccess: true);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C12");
            finding.Status.ShouldBe(FindingStatus.Pass);
        }

        [Fact]
        public void C12RefreshTokensDisabledWarns()
        {
            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.AuthorizationCode],
                allowOfflineAccess: false);

            var findings = _assessor.AssessClient(client);

            var finding = GetFinding(findings, "C12");
            finding.Status.ShouldBe(FindingStatus.Warning);
            finding.Recommendation!.ShouldContain("AllowOfflineAccess = true");
        }


    }

    public class CompleteConfigurationTests
    {
        [Fact]
        public void OAuth21CompliantServerHasAllPassesOrWarnings()
        {
            var options = CreateDefaultServerOptions(
                parEnabled: true,
                mtlsEnabled: true,
                signingAlgorithms: ["ES256", "PS256"],
                clockSkew: TimeSpan.FromMinutes(2),
                useHttp303Redirects: true);

            var assessor = new OAuth21Assessor(options);
            var findings = assessor.AssessServer();

            findings.ShouldNotBeEmpty();
            findings.Count.ShouldBe(8); // S01-S08
            findings.ShouldAllBe(f => f.Status == FindingStatus.Pass || f.Status == FindingStatus.Warning);
            findings.ShouldNotContain(f => f.Status == FindingStatus.Fail);
        }

        [Fact]
        public void OAuth21CompliantClientHasNoFailures()
        {
            var options = CreateDefaultServerOptions();
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
                authCodeLifetime: 30,
                allowOfflineAccess: true,
                refreshTokenUsage: ConformanceReportTokenUsage.OneTimeOnly);

            var findings = assessor.AssessClient(client);

            findings.ShouldNotBeEmpty();
            findings.Count.ShouldBe(12); // C01-C12
            findings.ShouldNotContain(f => f.Status == FindingStatus.Fail);
        }

        [Fact]
        public void MinimallyConfiguredClientHasMultipleWarnings()
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

            // Should have some warnings for: PAR not required, no sender constraint, no refresh tokens, shared secret
            var warnings = findings.Where(f => f.Status == FindingStatus.Warning).ToList();
            warnings.Count.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void NonCompliantClientWithImplicitGrantFails()
        {
            var options = CreateDefaultServerOptions();
            var assessor = new OAuth21Assessor(options);

            var client = CreateDefaultClient(
                grantTypes: [ConformanceReportGrantTypes.Implicit]);

            var findings = assessor.AssessClient(client);

            var grantTypeFinding = GetFinding(findings, "C01");
            grantTypeFinding.Status.ShouldBe(FindingStatus.Fail);
        }
    }
}
