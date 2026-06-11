// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Specialized;
using Duende.IdentityModel;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Validation;
using UnitTests.Common;
using UnitTests.Validation.Setup;

namespace IdentityServer.UnitTests.Licensing.V2;

public class IdentityServerLicenseValidatorTests
{
    // SKU constants matching SkuIds in Duende.Private.Licencing.V2
    private const string DPoP = "PTC-006";
    private const string ResourceIsolation = "IS-001";
    private const string Ciba = "PTC-022";
    private const string Par = "PTC-004";
    private const string DynamicProviders = "PLT-005";
    private const string ServerSideSessions = "PLT-021";
    private const string KeyManagement = "PLT-004";
    private const string SamlIdp = "PTC-010";
    private const string SamlIdpCount = "PTC-014";
    private const string SamlSp = "PTC-011";
    private const string SamlSpCount = "PTC-013";
    private const string ClientCount = "PTC-009";
    private const string IssuerCount = "PLT-020";

    [Fact]
    public void ValidateDPoP_returns_true_when_entitled()
    {
        var (sut, _) = TestLicense.CreateValidator(DPoP);
        sut.ValidateDPoP().ShouldBeTrue();
    }

    [Fact]
    public void ValidateDPoP_returns_false_when_not_entitled()
    {
        var (sut, _) = TestLicense.CreateValidator();
        sut.ValidateDPoP().ShouldBeFalse();
    }

    [Fact]
    public void ValidateCiba_returns_true_when_entitled()
    {
        var (sut, _) = TestLicense.CreateValidator(Ciba);
        sut.ValidateCiba().ShouldBeTrue();
    }

    [Fact]
    public void ValidateCiba_returns_false_when_not_entitled()
    {
        var (sut, _) = TestLicense.CreateValidator();
        sut.ValidateCiba().ShouldBeFalse();
    }

    [Fact]
    public void ValidatePar_returns_true_when_entitled()
    {
        var (sut, _) = TestLicense.CreateValidator(Par);
        sut.ValidatePar().ShouldBeTrue();
    }

    [Fact]
    public void ValidatePar_returns_false_when_not_entitled()
    {
        var (sut, _) = TestLicense.CreateValidator();
        sut.ValidatePar().ShouldBeFalse();
    }

    [Fact]
    public void ValidateDynamicProviders_returns_true_when_entitled()
    {
        var (sut, _) = TestLicense.CreateValidator(DynamicProviders);
        sut.ValidateDynamicProviders().ShouldBeTrue();
    }

    [Fact]
    public void ValidateDynamicProviders_returns_false_when_not_entitled()
    {
        var (sut, _) = TestLicense.CreateValidator();
        sut.ValidateDynamicProviders().ShouldBeFalse();
    }

    [Fact]
    public void ValidateServerSideSessions_returns_true_when_entitled()
    {
        var (sut, _) = TestLicense.CreateValidator(ServerSideSessions);
        sut.ValidateServerSideSessions().ShouldBeTrue();
    }

    [Fact]
    public void ValidateServerSideSessions_returns_false_when_not_entitled()
    {
        var (sut, _) = TestLicense.CreateValidator();
        sut.ValidateServerSideSessions().ShouldBeFalse();
    }

    [Fact]
    public void ValidateKeyManagement_returns_true_when_entitled()
    {
        var (sut, _) = TestLicense.CreateValidator(KeyManagement);
        sut.ValidateKeyManagement().ShouldBeTrue();
    }

    [Fact]
    public void ValidateKeyManagement_returns_false_when_not_entitled()
    {
        var (sut, _) = TestLicense.CreateValidator();
        sut.ValidateKeyManagement().ShouldBeFalse();
    }

    [Fact]
    public void ValidateSamlIdp_returns_true_when_entitled()
    {
        var (sut, _) = TestLicense.CreateValidator(SamlIdp);
        sut.ValidateSamlIdp().ShouldBeTrue();
    }

    [Fact]
    public void ValidateSamlIdp_returns_false_when_not_entitled()
    {
        var (sut, _) = TestLicense.CreateValidator();
        sut.ValidateSamlIdp().ShouldBeFalse();
    }

    [Fact]
    public void ValidateSamlServiceProvider_returns_true_when_entitled()
    {
        var (sut, _) = TestLicense.CreateValidator(SamlSp);
        sut.ValidateSamlServiceProvider().ShouldBeTrue();
    }

    [Fact]
    public void ValidateSamlServiceProvider_returns_false_when_not_entitled()
    {
        var (sut, _) = TestLicense.CreateValidator();
        sut.ValidateSamlServiceProvider().ShouldBeFalse();
    }

    // --- Quantized validation (client, issuer, SAML counts) ---

    [Fact]
    public void ValidateClient_tracks_unique_clients()
    {
        var sut = TestLicense.CreateValidatorWithLimit(ClientCount, 5);

        sut.ValidateClient("client-1");
        sut.ValidateClient("client-2");
        sut.ValidateClient("client-1"); // duplicate, should not increase count
    }

    [Fact]
    public void ValidateIssuer_tracks_unique_issuers()
    {
        var sut = TestLicense.CreateValidatorWithLimit(IssuerCount, 5);

        sut.ValidateIssuer("https://issuer1.example.com");
        sut.ValidateIssuer("https://issuer2.example.com");
        sut.ValidateIssuer("https://issuer1.example.com"); // duplicate
    }

    [Fact]
    public void ValidateSamlIdp_entityId_tracks_unique_idps()
    {
        var sut = TestLicense.CreateValidatorWithLimit(SamlSpCount, 5);

        sut.ValidateSamlIdp("https://idp1.example.com");
        sut.ValidateSamlIdp("https://idp2.example.com");
        sut.ValidateSamlIdp("https://idp1.example.com"); // duplicate

        var field = sut.GetType().GetField("_samlIdps",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var idps = field.GetValue(sut)!;
        var count = (int)idps.GetType().GetProperty("Count")!.GetValue(idps)!;
        count.ShouldBe(2);
    }

    [Fact]
    public void ValidateSamlServiceProvider_entityId_tracks_unique_sps()
    {
        var sut = TestLicense.CreateValidatorWithLimit(SamlSpCount, 5);

        sut.ValidateSamlServiceProvider("https://sp1.example.com");
        sut.ValidateSamlServiceProvider("https://sp2.example.com");
        sut.ValidateSamlServiceProvider("https://sp1.example.com"); // duplicate
    }

    [Fact]
    public void Validator_with_multiple_entitlements_validates_all()
    {
        var (sut, _) = TestLicense.CreateValidator(DPoP, Par, Ciba, KeyManagement);

        sut.ValidateDPoP().ShouldBeTrue();
        sut.ValidatePar().ShouldBeTrue();
        sut.ValidateCiba().ShouldBeTrue();
        sut.ValidateKeyManagement().ShouldBeTrue();

        // Features not in the entitlement list should fail
        sut.ValidateResourceIsolation().ShouldBeFalse();
        sut.ValidateDynamicProviders().ShouldBeFalse();
        sut.ValidateServerSideSessions().ShouldBeFalse();
    }

    [Fact]
    public void ValidateLicense_does_not_throw_for_valid_license()
    {
        var (sut, _) = TestLicense.CreateValidator(DPoP);

        // Should not throw — license is valid (expires in 1 year)
        sut.ValidateLicense();
    }

    [Fact]
    public async Task
        ValidateResourceIsolation_preserves_resource_indicators_when_no_license_configured()
    {
        var ct = TestContext.Current.CancellationToken;
        var mockResourceValidator = new MockResourceValidator
        {
            Result = new ResourceValidationResult
            {
                ParsedScopes =
                {
                    new ParsedScopeValue("openid"),
                    new ParsedScopeValue("scope1")
                }
            }
        };
        var clients = Factory.CreateClientStore();
        var client = await clients.FindEnabledClientByIdAsync("codeclient", ct);
        var grants = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            RedirectUri = "https://server/cb",
            RequestedScopes = new List<string>
            {
                "openid", "scope1"
            },
            RequestedResourceIndicators = new[] { "urn:finance" }
        };
        var (licenseValidator, logs) = TestLicense.CreateValidatorWithoutLicense();

        var handle = await grants.StoreAuthorizationCodeAsync(code, ct);
        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: grants,
            resourceValidator: mockResourceValidator,
            licenseValidator: licenseValidator);

        var parameters = new NameValueCollection
        {
            { OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode },
            { OidcConstants.TokenRequest.Code, handle },
            { OidcConstants.TokenRequest.RedirectUri, "https://server/cb" }
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), ct);

        result.IsError.ShouldBeFalse();
        mockResourceValidator.Request.ResourceIndicators.ShouldNotBeNull();
        mockResourceValidator.Request.ResourceIndicators.ShouldContain("urn:finance");

        var messages = logs.GetSnapshot().Select(r => r.Message).ToList();
        messages.ShouldNotContain(m => m.Contains("ignored", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task
        ValidateResourceIsolation_ignores_resource_indicators_when_licensed_without_entitlement()
    {
        var ct = TestContext.Current.CancellationToken;
        var mockResourceValidator = new MockResourceValidator
        {
            Result = new ResourceValidationResult
            {
                ParsedScopes =
                {
                    new ParsedScopeValue("openid"),
                    new ParsedScopeValue("scope1")
                }
            }
        };
        var clients = Factory.CreateClientStore();
        var client = await clients.FindEnabledClientByIdAsync("codeclient", ct);
        var grants = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            RedirectUri = "https://server/cb",
            RequestedScopes = new List<string>
            {
                "openid", "scope1"
            },
            RequestedResourceIndicators = new[] { "urn:finance" }
        };

        var (licenseValidator, logs) = TestLicense.CreateValidator();

        var handle = await grants.StoreAuthorizationCodeAsync(code, ct);
        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: grants,
            resourceValidator: mockResourceValidator,
            licenseValidator: licenseValidator);

        var parameters = new NameValueCollection
        {
            { OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode },
            { OidcConstants.TokenRequest.Code, handle },
            { OidcConstants.TokenRequest.RedirectUri, "https://server/cb" }
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), ct);

        result.IsError.ShouldBeFalse();
        mockResourceValidator.Request.ResourceIndicators.ShouldBeNull();
        result.ValidatedRequest.ValidatedResources.Resources.ApiResources.ShouldBeEmpty();

        var messages = logs.GetSnapshot().Select(r => r.Message).ToList();
        messages.ShouldContain(m => m.Contains("ignored", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task
        ValidateResourceIsolation_preserves_resource_indicators_when_licensed_with_entitlement()
    {
        var ct = TestContext.Current.CancellationToken;
        var mockResourceValidator = new MockResourceValidator
        {
            Result = new ResourceValidationResult
            {
                ParsedScopes =
                {
                    new ParsedScopeValue("openid"),
                    new ParsedScopeValue("scope1")
                }
            }
        };
        var clients = Factory.CreateClientStore();
        var client = await clients.FindEnabledClientByIdAsync("codeclient", ct);
        var grants = Factory.CreateAuthorizationCodeStore();

        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            RedirectUri = "https://server/cb",
            RequestedScopes = new List<string>
            {
                "openid", "scope1"
            },
            RequestedResourceIndicators = new[] { "urn:finance" }
        };

        var (licenseValidator, logs) = TestLicense.CreateValidator(ResourceIsolation);

        var handle = await grants.StoreAuthorizationCodeAsync(code, ct);
        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: grants,
            resourceValidator: mockResourceValidator,
            licenseValidator: licenseValidator);

        var parameters = new NameValueCollection
        {
            { OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode },
            { OidcConstants.TokenRequest.Code, handle },
            { OidcConstants.TokenRequest.RedirectUri, "https://server/cb" }
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), ct);

        result.IsError.ShouldBeFalse();
        mockResourceValidator.Request.ResourceIndicators.ShouldNotBeNull();
        mockResourceValidator.Request.ResourceIndicators.ShouldContain("urn:finance");

        var messages = logs.GetSnapshot().Select(r => r.Message).ToList();
        messages.ShouldNotContain(m => m.Contains("ignored", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task
        ValidateResourceIsolation_strips_requested_indicator_on_token_exchange_when_original_had_none_and_not_entitled()
    {
        var ct = TestContext.Current.CancellationToken;
        var mockResourceValidator = new MockResourceValidator
        {
            Result = new ResourceValidationResult
            {
                ParsedScopes =
                {
                    new ParsedScopeValue("openid"),
                    new ParsedScopeValue("scope1")
                }
            }
        };
        var clients = Factory.CreateClientStore();
        var client = await clients.FindEnabledClientByIdAsync("codeclient", ct);
        var grants = Factory.CreateAuthorizationCodeStore();

        // Original authorize request had NO resource indicators
        var code = new AuthorizationCode
        {
            CreationTime = DateTime.UtcNow,
            Subject = new IdentityServerUser("123").CreatePrincipal(),
            ClientId = client.ClientId,
            Lifetime = client.AuthorizationCodeLifetime,
            RedirectUri = "https://server/cb",
            RequestedScopes = new List<string>
            {
                "openid", "scope1"
            }
        };

        var (licenseValidator, _) = TestLicense.CreateValidator();

        var handle = await grants.StoreAuthorizationCodeAsync(code, ct);
        var validator = Factory.CreateTokenRequestValidator(
            authorizationCodeStore: grants,
            resourceValidator: mockResourceValidator,
            licenseValidator: licenseValidator);

        // Token exchange sends a resource indicator not in the original request
        var parameters = new NameValueCollection
        {
            { OidcConstants.TokenRequest.GrantType, OidcConstants.GrantTypes.AuthorizationCode },
            { OidcConstants.TokenRequest.Code, handle },
            { OidcConstants.TokenRequest.RedirectUri, "https://server/cb" },
            { OidcConstants.AuthorizeRequest.Resource, "urn:finance" }
        };

        var result = await validator.ValidateRequestAsync(parameters, client.ToValidationResult(), ct);

        result.IsError.ShouldBeFalse();
        // The requested resource indicator should have been stripped by the license check
        result.ValidatedRequest.RequestedResourceIndicator.ShouldBeNullOrEmpty();
    }
}
