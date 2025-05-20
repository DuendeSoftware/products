// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel.Client;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Microsoft.Extensions.Options;

namespace IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

public class IdentityServerOptionsDiagnosticEntryTests
{
    [Fact]
    public async Task WriteAsync_ShouldExcludeLicenseKey()
    {
        var options = new IdentityServerOptions
        {
            LicenseKey = "test-key"
        };
        var subject = new IdentityServerOptionsDiagnosticEntry(Options.Create(options));

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        result.RootElement.GetProperty("IdentityServerOptions").TryGetProperty("LicenseKey", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task WriteAsync_ShouldIncludeOtherProperties()
    {
        var options = new IdentityServerOptions
        {
            IssuerUri = "https://example.com",
            LowerCaseIssuerUri = true,
            AccessTokenJwtType = "jwt",
            LogoutTokenJwtType = "logout",
            EmitStaticAudienceClaim = true,
            EmitScopesAsSpaceDelimitedStringInJwt = true,
            EmitIssuerIdentificationResponseParameter = false,
            EmitStateHash = true,
            StrictJarValidation = true,
            ValidateTenantOnAuthorization = true,
            JwtValidationClockSkew = TimeSpan.FromMinutes(1),
            AllowedJwtAlgorithms = ["SHA256", "SHA512"],
            DiagnosticSummaryLogFrequency = TimeSpan.FromMinutes(30)
        };
        var subject = new IdentityServerOptionsDiagnosticEntry(Options.Create(options));

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var identityServerOptions = result.RootElement.GetProperty("IdentityServerOptions");
        identityServerOptions.GetProperty("IssuerUri").GetString().ShouldBe("https://example.com");
        identityServerOptions.GetProperty("LowerCaseIssuerUri").GetBoolean().ShouldBeTrue();
        identityServerOptions.GetProperty("AccessTokenJwtType").GetString().ShouldBe("jwt");
        identityServerOptions.GetProperty("LogoutTokenJwtType").GetString().ShouldBe("logout");
        identityServerOptions.GetProperty("EmitStaticAudienceClaim").GetBoolean().ShouldBeTrue();
        identityServerOptions.GetProperty("EmitScopesAsSpaceDelimitedStringInJwt").GetBoolean().ShouldBeTrue();
        identityServerOptions.GetProperty("EmitIssuerIdentificationResponseParameter").GetBoolean().ShouldBeFalse();
        identityServerOptions.GetProperty("EmitStateHash").GetBoolean().ShouldBeTrue();
        identityServerOptions.GetProperty("StrictJarValidation").GetBoolean().ShouldBeTrue();
        identityServerOptions.GetProperty("ValidateTenantOnAuthorization").GetBoolean().ShouldBeTrue();
        identityServerOptions.TryGetProperty("Endpoints", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Discovery", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Authentication", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Events", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("InputLengthRestrictions", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("UserInteraction", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Caching", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Cors", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Csp", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Validation", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("DeviceFlow", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Ciba", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("Logging", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("MutualTls", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("KeyManagement", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("PersistentGrants", out _).ShouldBeTrue();
        identityServerOptions.TryGetProperty("DPoP", out _).ShouldBeTrue();
        identityServerOptions.GetProperty("JwtValidationClockSkew").GetString().ShouldBe(TimeSpan.FromMinutes(1).ToString());
        var allowedJwtAlgorithms = identityServerOptions.TryGetStringArray("AllowedJwtAlgorithms");
        allowedJwtAlgorithms.ShouldBe(["SHA256", "SHA512"]);
        identityServerOptions.TryGetProperty("Preview", out _).ShouldBeTrue();
        identityServerOptions.GetProperty("DiagnosticSummaryLogFrequency").GetString().ShouldBe(TimeSpan.FromMinutes(30).ToString());
    }
}
