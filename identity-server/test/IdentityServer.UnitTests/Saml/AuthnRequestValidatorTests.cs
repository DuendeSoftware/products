// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Xml;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Bindings;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Saml.Samlp;
using Duende.IdentityServer.Saml.Services.Default;
using Duende.IdentityServer.Saml.Validation;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.Common;
using SamlpNs = Duende.IdentityServer.Saml.Samlp;

namespace UnitTests.Saml;

public sealed class AuthnRequestValidatorTests
{
    private const string Category = "AuthnRequest Validator";
    private const string IdpOrigin = "https://idp.example.com";
    private const string SsoPath = "/Saml2/SSO";
    private const string ExpectedDestination = IdpOrigin + SsoPath;

    private static readonly DateTimeOffset DefaultNow = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Category", Category)]
    public async Task ValidVersion20Succeeds()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(version: "2.0");

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task InvalidVersionReturnsVersionMismatch()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(version: "1.1");

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.VersionMismatch);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task IssueInstantWithinClockSkewSucceeds()
    {
        var now = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var validator = CreateValidator(timeProvider: timeProvider);
        var request = CreateValidatedAuthnRequest(issueInstant: now.UtcDateTime);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task IssueInstantInFutureBeyondClockSkewFails()
    {
        var now = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var clockSkew = TimeSpan.FromMinutes(5);
        var timeProvider = new FakeTimeProvider(now);
        var samlOptions = new SamlOptions { DefaultClockSkew = clockSkew };
        var validator = CreateValidator(timeProvider: timeProvider, samlOptions: samlOptions);
        var request = CreateValidatedAuthnRequest(issueInstant: now.UtcDateTime.Add(clockSkew).AddSeconds(1));

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
        result.ErrorDescription!.ShouldContain("future");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task IssueInstantExpiredBeyondMaxAgeFails()
    {
        var now = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var maxAge = TimeSpan.FromMinutes(5);
        var timeProvider = new FakeTimeProvider(now);
        var samlOptions = new SamlOptions { DefaultRequestMaxAge = maxAge };
        var validator = CreateValidator(timeProvider: timeProvider, samlOptions: samlOptions);
        var request = CreateValidatedAuthnRequest(issueInstant: now.UtcDateTime.Add(-maxAge).AddSeconds(-1));

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
        result.ErrorDescription!.ShouldContain("expired");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task IssueInstantAtExactClockSkewBoundarySucceeds()
    {
        var now = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var clockSkew = TimeSpan.FromMinutes(5);
        var timeProvider = new FakeTimeProvider(now);
        var samlOptions = new SamlOptions { DefaultClockSkew = clockSkew };
        var validator = CreateValidator(timeProvider: timeProvider, samlOptions: samlOptions);
        var request = CreateValidatedAuthnRequest(issueInstant: now.UtcDateTime.Add(clockSkew));

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SpSpecificClockSkewOverridesDefault()
    {
        var now = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var defaultClockSkew = TimeSpan.FromMinutes(3);
        var spClockSkew = TimeSpan.FromMinutes(10);
        var timeProvider = new FakeTimeProvider(now);
        var samlOptions = new SamlOptions { DefaultClockSkew = defaultClockSkew };
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            ClockSkew = spClockSkew,
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new()
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost
                }
            }
        };
        var validator = CreateValidator(sp: sp, timeProvider: timeProvider, samlOptions: samlOptions);
        var request = CreateValidatedAuthnRequest(
            issueInstant: now.UtcDateTime.Add(defaultClockSkew).AddMinutes(1));

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SpSpecificMaxAgeOverridesDefault()
    {
        var now = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var defaultMaxAge = TimeSpan.FromMinutes(3);
        var spMaxAge = TimeSpan.FromMinutes(10);
        var timeProvider = new FakeTimeProvider(now);
        var samlOptions = new SamlOptions { DefaultRequestMaxAge = defaultMaxAge };
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            RequestMaxAge = spMaxAge,
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new()
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost
                }
            }
        };
        var validator = CreateValidator(sp: sp, timeProvider: timeProvider, samlOptions: samlOptions);
        var request = CreateValidatedAuthnRequest(
            issueInstant: now.UtcDateTime.Add(-defaultMaxAge).AddMinutes(-1));

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task NullDestinationUnsignedRequestSucceeds()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(destination: null);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task NullDestinationSignedRequestFails()
    {
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            RequireSignedAuthnRequests = true,
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new()
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost
                }
            }
        };
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedAuthnRequest(destination: null, trustLevel: TrustLevel.ConfiguredKey);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
        result.ErrorDescription!.ShouldContain("Destination");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task CorrectDestinationSucceeds()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(destination: ExpectedDestination);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task WrongDestinationFails()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(destination: "https://evil.example.com/Saml2/SSO");

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
        result.ErrorDescription!.ShouldContain("destination");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task WrongDestinationErrorDoesNotLeakExpectedUrl()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(destination: "https://evil.example.com/Saml2/SSO");

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.ErrorDescription!.ShouldNotContain(IdpOrigin);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ValidAcsUrlSucceeds()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(acsUrl: "https://sp.example.com/acs");

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.AssertionConsumerService.ShouldNotBeNull();
        result.ValidatedRequest.AssertionConsumerService.Location.ShouldBe("https://sp.example.com/acs");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnregisteredAcsUrlFails()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(acsUrl: "https://evil.example.com/acs");

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
        result.ErrorDescription!.ShouldContain("not registered");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task MalformedAcsUrlFails()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(acsUrl: "not-a-valid-uri");

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
        result.ErrorDescription!.ShouldContain("not a valid absolute URI");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task NoAcsUrlDefaultsToFirstConfigured()
    {
        var firstUrl = new IndexedEndpoint
        {
            Location = "https://sp.example.com/acs",
            Binding = SamlBinding.HttpPost
        };
        var secondUrl = new IndexedEndpoint
        {
            Location = "https://sp.example.com/acs2",
            Binding = SamlBinding.HttpPost
        };
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            AssertionConsumerServiceUrls = new List<IndexedEndpoint> { firstUrl, secondUrl }
        };
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedAuthnRequest();

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.AssertionConsumerService.ShouldBe(firstUrl);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task AcsUrlErrorDoesNotLeakUrl()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(acsUrl: "https://evil.example.com/acs");

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.ErrorDescription!.ShouldNotContain("evil.example.com");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task BothAcsUrlAndIndexProvidedFails()
    {
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            AssertionConsumerServiceUrls = new List<IndexedEndpoint>
            {
                new() { Location = "https://sp.example.com/acs", Binding = SamlBinding.HttpPost, Index = 0 }
            }
        };
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedAuthnRequest(acsUrl: "https://sp.example.com/acs", acsIndex: 0);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
        result.ErrorDescription!.ShouldContain("Both");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task AcsUrlWithMatchingProtocolBindingSucceeds()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(
            acsUrl: "https://sp.example.com/acs",
            protocolBinding: SamlBinding.HttpPost);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.AssertionConsumerService.ShouldNotBeNull();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task AcsUrlWithUnmatchedProtocolBindingFallsBackToRegisteredEndpoint()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(
            acsUrl: "https://sp.example.com/acs",
            protocolBinding: SamlBinding.HttpRedirect);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.AssertionConsumerService.ShouldNotBeNull();
        result.ValidatedRequest.AssertionConsumerService.Binding.ShouldBe(SamlBinding.HttpPost);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task AcsUrlWithNoProtocolBindingInRequestSucceeds()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(acsUrl: "https://sp.example.com/acs");

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task AcsIndexResolvesToRegisteredEndpoint()
    {
        var acs = new IndexedEndpoint
        {
            Location = "https://sp.example.com/acs",
            Binding = SamlBinding.HttpPost,
            Index = 3
        };
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            AssertionConsumerServiceUrls = new List<IndexedEndpoint> { acs }
        };
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedAuthnRequest(acsIndex: 3);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.AssertionConsumerService.ShouldBe(acs);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task AcsIndexNotFoundFails()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(acsIndex: 99);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
        result.ErrorDescription!.ShouldContain("index");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task NoAcsUrlOrIndexPrefersIsDefaultEndpoint()
    {
        var nonDefault = new IndexedEndpoint
        {
            Location = "https://sp.example.com/acs1",
            Binding = SamlBinding.HttpPost,
            Index = 0
        };
        var defaultAcs = new IndexedEndpoint
        {
            Location = "https://sp.example.com/acs2",
            Binding = SamlBinding.HttpPost,
            Index = 1,
            IsDefault = true
        };
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            AssertionConsumerServiceUrls = new List<IndexedEndpoint> { nonDefault, defaultAcs }
        };
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedAuthnRequest();

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.AssertionConsumerService.ShouldBe(defaultAcs);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task DuplicateLocationWithMatchingProtocolBindingSelectsCorrectEndpoint()
    {
        var postAcs = new IndexedEndpoint
        {
            Location = "https://sp.example.com/acs",
            Binding = SamlBinding.HttpPost,
            Index = 0
        };
        var redirectAcs = new IndexedEndpoint
        {
            Location = "https://sp.example.com/acs",
            Binding = SamlBinding.HttpRedirect,
            Index = 1
        };
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            AssertionConsumerServiceUrls = new List<IndexedEndpoint> { postAcs, redirectAcs }
        };
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedAuthnRequest(
            acsUrl: "https://sp.example.com/acs",
            protocolBinding: SamlBinding.HttpRedirect);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.AssertionConsumerService.ShouldBe(redirectAcs);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task DuplicateLocationWithNoProtocolBindingSelectsDefault()
    {
        var postAcs = new IndexedEndpoint
        {
            Location = "https://sp.example.com/acs",
            Binding = SamlBinding.HttpPost,
            Index = 0
        };
        var redirectAcs = new IndexedEndpoint
        {
            Location = "https://sp.example.com/acs",
            Binding = SamlBinding.HttpRedirect,
            Index = 1,
            IsDefault = true
        };
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            AssertionConsumerServiceUrls = new List<IndexedEndpoint> { postAcs, redirectAcs }
        };
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedAuthnRequest(acsUrl: "https://sp.example.com/acs");

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.AssertionConsumerService.ShouldBe(redirectAcs);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task DuplicateLocationWithNoProtocolBindingAndNoDefaultSelectsFirst()
    {
        var postAcs = new IndexedEndpoint
        {
            Location = "https://sp.example.com/acs",
            Binding = SamlBinding.HttpPost,
            Index = 0
        };
        var redirectAcs = new IndexedEndpoint
        {
            Location = "https://sp.example.com/acs",
            Binding = SamlBinding.HttpRedirect,
            Index = 1
        };
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            AssertionConsumerServiceUrls = new List<IndexedEndpoint> { postAcs, redirectAcs }
        };
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedAuthnRequest(acsUrl: "https://sp.example.com/acs");

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.AssertionConsumerService.ShouldBe(postAcs);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task DuplicateLocationWithUnmatchedProtocolBindingFallsBackToDefault()
    {
        var postAcs = new IndexedEndpoint
        {
            Location = "https://sp.example.com/acs",
            Binding = SamlBinding.HttpPost,
            Index = 0
        };
        var redirectAcs = new IndexedEndpoint
        {
            Location = "https://sp.example.com/acs",
            Binding = SamlBinding.HttpRedirect,
            Index = 1,
            IsDefault = true
        };
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            AssertionConsumerServiceUrls = new List<IndexedEndpoint> { postAcs, redirectAcs }
        };
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedAuthnRequest(
            acsUrl: "https://sp.example.com/acs");
        // Set an unrecognized protocol binding directly — cannot use the helper parameter
        // because ToUrn() throws for unknown SamlBinding values.
        request.AuthnRequest!.ProtocolBinding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Artifact";

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.AssertionConsumerService.ShouldBe(redirectAcs);
    }


    [Fact]
    [Trait("Category", Category)]
    public async Task SignatureRequiredAndTrustedSucceeds()
    {
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            RequireSignedAuthnRequests = true,
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new()
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost
                }
            }
        };
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedAuthnRequest(trustLevel: TrustLevel.ConfiguredKey);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SignatureRequiredButUntrustedFails()
    {
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            RequireSignedAuthnRequests = true,
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new()
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost
                }
            }
        };
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedAuthnRequest(trustLevel: TrustLevel.None);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
        result.ErrorDescription!.ShouldContain("signature");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SignatureRequiredButTlsTrustOnlyFails()
    {
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            RequireSignedAuthnRequests = true,
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new()
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost
                }
            }
        };
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedAuthnRequest(trustLevel: TrustLevel.TLS);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
        result.ErrorDescription.ShouldBe("The AuthnRequest signature is missing or not trusted");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SignatureNotRequiredAndUntrustedSucceeds()
    {
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            RequireSignedAuthnRequests = false,
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new()
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost
                }
            }
        };
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedAuthnRequest(trustLevel: TrustLevel.None);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SignatureRequiredErrorDoesNotLeakTrustDetails()
    {
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            RequireSignedAuthnRequests = true,
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new()
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost
                }
            }
        };
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedAuthnRequest(trustLevel: TrustLevel.None);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.ErrorDescription.ShouldBe("The AuthnRequest signature is missing or not trusted");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task GlobalWantAuthnRequestsSignedEnforcedWhenSpDoesNotOverride()
    {
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            // RequireSignedAuthnRequests is null — should fall back to global
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new()
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost
                }
            }
        };
        var validator = CreateValidator(sp: sp, wantAuthnRequestsSigned: true);
        var request = CreateValidatedAuthnRequest(trustLevel: TrustLevel.None);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.ErrorDescription.ShouldBe("The AuthnRequest signature is missing or not trusted");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SpCanOverrideGlobalWantAuthnRequestsSignedToFalse()
    {
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            RequireSignedAuthnRequests = false,
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new()
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost
                }
            }
        };
        var validator = CreateValidator(sp: sp, wantAuthnRequestsSigned: true);
        var request = CreateValidatedAuthnRequest(trustLevel: TrustLevel.None);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task NullNameIdFormatSucceeds()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest();

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task SupportedNameIdFormatSucceeds()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(
            nameIdPolicy: new SamlpNs.NameIdPolicy { Format = SamlConstants.NameIdentifierFormats.EmailAddress });

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task UnsupportedNameIdFormatReturnsError()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(
            nameIdPolicy: new SamlpNs.NameIdPolicy { Format = "urn:oasis:names:tc:SAML:1.1:nameid-format:X509SubjectName" });

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RequestWithoutScopingSucceeds()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest();

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RequestWithScopingReturnsError()
    {
        var validator = CreateValidator();
        var request = CreateValidatedAuthnRequest(scoping: new SamlpNs.Scoping());

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Requester);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RequestWithEmptyAllowedScopesReturnsError()
    {
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string>(),
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new() { Location = "https://sp.example.com/acs", Binding = SamlBinding.HttpPost }
            }
        };
        var validator = CreateValidator(sp: sp);
        var request = CreateValidatedAuthnRequest();

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Responder);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RequestWithValidAllowedScopesPopulatesValidatedResources()
    {
        var identityResource = new IdentityResource("profile", ["name", "email"]);
        var resourceStore = new InMemoryResourcesStore([identityResource], null, null);
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "profile" },
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new() { Location = "https://sp.example.com/acs", Binding = SamlBinding.HttpPost }
            }
        };
        var validator = CreateValidator(sp: sp, resourceStore: resourceStore);
        var request = CreateValidatedAuthnRequest();

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.ValidatedResources.ShouldNotBeNull();
        result.ValidatedRequest.ValidatedResources.Resources.IdentityResources
            .ShouldContain(r => r.Name == "profile");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RequestWithInvalidAllowedScopeReturnsError()
    {
        var resourceStore = new InMemoryResourcesStore(null, null, null);
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "nonexistent" },
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new() { Location = "https://sp.example.com/acs", Binding = SamlBinding.HttpPost }
            }
        };
        var validator = CreateValidator(sp: sp, resourceStore: resourceStore);
        var request = CreateValidatedAuthnRequest();

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Responder);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RequestWithValidRequestedClaimTypesSucceeds()
    {
        var identityResource = new IdentityResource("profile", ["name", "email"]);
        var resourceStore = new InMemoryResourcesStore([identityResource], null, null);
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "profile" },
            RequestedClaimTypes = ["name"],
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new() { Location = "https://sp.example.com/acs", Binding = SamlBinding.HttpPost }
            }
        };
        var validator = CreateValidator(sp: sp, resourceStore: resourceStore);
        var request = CreateValidatedAuthnRequest();

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.RequestedClaimTypes.ShouldBe(["name"]);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RequestWithEmptyRequestedClaimTypesFallsBackToAllClaimsFromScopes()
    {
        var identityResource = new IdentityResource("profile", ["name", "email"]);
        var resourceStore = new InMemoryResourcesStore([identityResource], null, null);
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "profile" },
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new() { Location = "https://sp.example.com/acs", Binding = SamlBinding.HttpPost }
            }
        };
        var validator = CreateValidator(sp: sp, resourceStore: resourceStore);
        var request = CreateValidatedAuthnRequest();

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeFalse();
        result.ValidatedRequest.RequestedClaimTypes.ShouldContain("name");
        result.ValidatedRequest.RequestedClaimTypes.ShouldContain("email");
        result.ValidatedRequest.RequestedClaimTypes.Count.ShouldBe(2);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task RequestWithRequestedClaimTypeNotInAllowedScopesReturnsError()
    {
        var identityResource = new IdentityResource("profile", ["name", "email"]);
        var resourceStore = new InMemoryResourcesStore([identityResource], null, null);
        var sp = new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "profile" },
            RequestedClaimTypes = ["name", "phone_number"],
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new() { Location = "https://sp.example.com/acs", Binding = SamlBinding.HttpPost }
            }
        };
        var validator = CreateValidator(sp: sp, resourceStore: resourceStore);
        var request = CreateValidatedAuthnRequest();

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.Error.ShouldBe(SamlStatusCodes.Responder);
    }

    private static AuthnRequestValidator CreateValidator(
        SamlServiceProvider? sp = null,
        TimeProvider? timeProvider = null,
        SamlOptions? samlOptions = null,
        bool wantAuthnRequestsSigned = false,
        IResourceStore? resourceStore = null)
    {
        sp ??= new SamlServiceProvider
        {
            EntityId = "https://sp.example.com",
            Enabled = true,
            AllowedScopes = new HashSet<string> { "openid" },
            AssertionConsumerServiceUrls = new HashSet<IndexedEndpoint>
            {
                new()
                {
                    Location = "https://sp.example.com/acs",
                    Binding = SamlBinding.HttpPost
                }
            }
        };
        var store = new InMemorySamlServiceProviderStore([sp]);
        var serverUrls = new MockServerUrls { Origin = IdpOrigin };
        var opts = samlOptions ?? new SamlOptions();
        opts.WantAuthnRequestsSigned = wantAuthnRequestsSigned;
        opts.Endpoints.SingleSignOnServicePath = SsoPath;
        var defaultResourceStore = new InMemoryResourcesStore(
            [new IdentityResource("openid", ["sub"])], null, null);
        var resolver = new DefaultSamlResourceResolver(
            resourceStore ?? defaultResourceStore,
            NullLogger<DefaultSamlResourceResolver>.Instance);
        var idServerOptions = Microsoft.Extensions.Options.Options.Create(new IdentityServerOptions { Saml = opts });
        return new AuthnRequestValidator(
            store,
            resolver,
            timeProvider ?? new FakeTimeProvider(DefaultNow),
            idServerOptions,
            serverUrls,
            NullLogger<AuthnRequestValidator>.Instance);
    }

    private static ValidatedAuthnRequest CreateValidatedAuthnRequest(
        string version = SamlVersions.V2,
        DateTime issueInstant = default,
        string? destination = ExpectedDestination,
        string? acsUrl = null,
        int? acsIndex = null,
        SamlBinding? protocolBinding = null,
        TrustLevel trustLevel = TrustLevel.None,
        SamlpNs.NameIdPolicy? nameIdPolicy = null,
        SamlpNs.Scoping? scoping = null)
    {
        var xmlDoc = new XmlDocument();
        var xmlElement = xmlDoc.CreateElement("SAMLRequest");

        return new ValidatedAuthnRequest
        {
            IdentityServerOptions = new IdentityServerOptions(),
            AuthnRequest = new AuthnRequest
            {
                Version = version,
                IssueInstant = issueInstant == default
                    ? new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc)
                    : issueInstant,
                Issuer = new NameId("https://sp.example.com"),
                Destination = destination,
                AssertionConsumerServiceUrl = acsUrl,
                AssertionConsumerServiceIndex = acsIndex,
                ProtocolBinding = protocolBinding?.ToUrn(),
                TrustLevel = trustLevel,
                NameIdPolicy = nameIdPolicy,
                Scoping = scoping
            },
            Binding = SamlBinding.HttpPost.ToUrn(),
            Saml2Message = new InboundSaml2Message
            {
                Name = "SAMLRequest",
                Xml = xmlElement,
                Destination = "https://idp.example.com/Saml2/SSO",
                Binding = SamlBinding.HttpPost.ToUrn()
            },
            Saml2IdpEntityId = "https://idp.example.com"
        };
    }
}
