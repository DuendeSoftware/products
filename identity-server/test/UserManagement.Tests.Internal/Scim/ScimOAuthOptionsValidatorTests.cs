// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces;
using Duende.UserManagement.Scim;
using Duende.UserManagement.Scim.Internal;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimOAuthOptionsValidatorTests
{
    private readonly ScimOAuthOptionsValidator _sut = new();

    private sealed class FakeSpaceContextAccessor : ISpaceContextAccessor
    {
        public SpaceId GetSpaceId() => SpaceId.Default;
        public bool IsSpaceIdConfigured() => true;
        public IDisposable SetSpace(SpaceId spaceId) => throw new NotSupportedException();
    }

    [Fact]
    public void SucceedsWhenAuthorityIsSet()
    {
        var options = new ScimOAuthOptions { Authority = "https://example.com" };

        var result = _sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void FailsWhenAuthorityIsNullAndNoCustomPolicy()
    {
        var options = new ScimOAuthOptions { Authority = null };

        var result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Authority must be configured");
    }

    [Fact]
    public void FailsWhenAuthorityIsEmptyAndNoCustomPolicy()
    {
        var options = new ScimOAuthOptions { Authority = "" };

        var result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Authority must be configured");
    }

    [Fact]
    public void SucceedsWhenCustomPolicyNameSetAndAuthorityIsNull()
    {
        var options = new ScimOAuthOptions
        {
            AuthorizationPolicyName = "my-policy",
            Authority = null
        };

        var result = _sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void SucceedsWhenCustomPolicyNameSetAndAuthorityIsEmpty()
    {
        var options = new ScimOAuthOptions
        {
            AuthorizationPolicyName = "my-policy",
            Authority = ""
        };

        var result = _sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void FailsWhenAuthorityIsWhitespaceAndNoCustomPolicy()
    {
        var options = new ScimOAuthOptions { Authority = "   " };

        var result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Authority must be configured");
    }

    [Fact]
    public void FailsWhenAudienceIsEmptyAndNoCustomPolicy()
    {
        var options = new ScimOAuthOptions
        {
            Authority = "https://example.com",
            Audience = ""
        };

        var result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Audience must not be empty");
    }

    [Fact]
    public void FailsWhenAudienceIsWhitespaceAndNoCustomPolicy()
    {
        var options = new ScimOAuthOptions
        {
            Authority = "https://example.com",
            Audience = "   "
        };

        var result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Audience must not be empty");
    }

    [Fact]
    public void SkipsAudienceValidationWhenCustomPolicyIsSet()
    {
        var options = new ScimOAuthOptions
        {
            AuthorizationPolicyName = "my-policy",
            Audience = ""
        };

        var result = _sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void SucceedsWhenLocalIssuerMarkerPresentAndAuthorityIsBlank()
    {
        var sut = new ScimOAuthOptionsValidator(new ScimLocalIssuerMarker());
        var options = new ScimOAuthOptions { Authority = null };

        var result = sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void FailsWhenNoLocalIssuerMarkerAndAuthorityIsBlank()
    {
        var sut = new ScimOAuthOptionsValidator();
        var options = new ScimOAuthOptions { Authority = null };

        var result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Authority must be configured");
    }

    [Fact]
    public void FailsWhenSpacesRegisteredAndAuthoritySetWithBuiltInPolicy()
    {
        var sut = new ScimOAuthOptionsValidator(spaces: new FakeSpaceContextAccessor());
        var options = new ScimOAuthOptions { Authority = "https://example.com" };

        var result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(
            "ScimOAuthOptions.Authority can't be combined with Spaces and the built-in SCIM policies");
    }

    [Fact]
    public void SucceedsWhenSpacesRegisteredAndAuthoritySetWithCustomPolicy()
    {
        var sut = new ScimOAuthOptionsValidator(spaces: new FakeSpaceContextAccessor());
        var options = new ScimOAuthOptions
        {
            Authority = "https://example.com",
            AuthorizationPolicyName = "my-policy"
        };

        var result = sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void SucceedsWhenNoSpacesAndAuthoritySet()
    {
        var sut = new ScimOAuthOptionsValidator();
        var options = new ScimOAuthOptions { Authority = "https://example.com" };

        var result = sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void SucceedsWhenBothReadAndWritePolicyNamesSetAndAuthorityBlank()
    {
        var sut = new ScimOAuthOptionsValidator();
        var options = new ScimOAuthOptions
        {
            Authority = null,
            ReadAuthorizationPolicyName = "my-read-policy",
            WriteAuthorizationPolicyName = "my-write-policy"
        };

        var result = sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void FailsWhenOnlyReadPolicyNameSetAndAuthorityBlankOutsideIdentityServer()
    {
        var sut = new ScimOAuthOptionsValidator();
        var options = new ScimOAuthOptions
        {
            Authority = null,
            ReadAuthorizationPolicyName = "my-read-policy"
        };

        var result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Authority must be configured");
    }

    [Fact]
    public void FailsWhenOnlyWritePolicyNameSetAndAuthorityBlankOutsideIdentityServer()
    {
        var sut = new ScimOAuthOptionsValidator();
        var options = new ScimOAuthOptions
        {
            Authority = null,
            WriteAuthorizationPolicyName = "my-write-policy"
        };

        var result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Authority must be configured");
    }

    [Fact]
    public void FailsWhenSpacesRegisteredAndAuthoritySetWithOnlyReadPolicyCustom()
    {
        var sut = new ScimOAuthOptionsValidator(spaces: new FakeSpaceContextAccessor());
        var options = new ScimOAuthOptions
        {
            Authority = "https://example.com",
            ReadAuthorizationPolicyName = "my-read-policy"
        };

        var result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(
            "ScimOAuthOptions.Authority can't be combined with Spaces and the built-in SCIM policies");
    }

    [Fact]
    public void FailsWhenSpacesRegisteredAndAuthoritySetWithOnlyWritePolicyCustom()
    {
        var sut = new ScimOAuthOptionsValidator(spaces: new FakeSpaceContextAccessor());
        var options = new ScimOAuthOptions
        {
            Authority = "https://example.com",
            WriteAuthorizationPolicyName = "my-write-policy"
        };

        var result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(
            "ScimOAuthOptions.Authority can't be combined with Spaces and the built-in SCIM policies");
    }

    [Fact]
    public void SucceedsWhenSpacesRegisteredAndAuthoritySetWithBothReadAndWritePolicyCustom()
    {
        var sut = new ScimOAuthOptionsValidator(spaces: new FakeSpaceContextAccessor());
        var options = new ScimOAuthOptions
        {
            Authority = "https://example.com",
            ReadAuthorizationPolicyName = "my-read-policy",
            WriteAuthorizationPolicyName = "my-write-policy"
        };

        var result = sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void FailsWhenSpacesRegisteredAndAuthoritySetWithExplicitBuiltInPolicyNames()
    {
        var sut = new ScimOAuthOptionsValidator(spaces: new FakeSpaceContextAccessor());
        var options = new ScimOAuthOptions
        {
            Authority = "https://example.com",
            ReadAuthorizationPolicyName = "DuendeScimRead",
            WriteAuthorizationPolicyName = "DuendeScimWrite"
        };

        var result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(
            "ScimOAuthOptions.Authority can't be combined with Spaces and the built-in SCIM policies");
    }

    [Fact]
    public void FailsWhenSpacesRegisteredAndAuthoritySetWithExplicitBuiltInPolicyNamesDifferentCasing()
    {
        var sut = new ScimOAuthOptionsValidator(spaces: new FakeSpaceContextAccessor());
        var options = new ScimOAuthOptions
        {
            Authority = "https://example.com",
            ReadAuthorizationPolicyName = "duendescimread",
            WriteAuthorizationPolicyName = "DUENDESCIMWRITE"
        };

        var result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(
            "ScimOAuthOptions.Authority can't be combined with Spaces and the built-in SCIM policies");
    }

    [Fact]
    public void FailsWhenSpacesRegisteredAndAuthoritySetWithSharedAuthorizationPolicyNameEqualToBuiltIn()
    {
        var sut = new ScimOAuthOptionsValidator(spaces: new FakeSpaceContextAccessor());
        var options = new ScimOAuthOptions
        {
            Authority = "https://example.com",
            AuthorizationPolicyName = "DuendeScimWrite"
        };

        var result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(
            "ScimOAuthOptions.Authority can't be combined with Spaces and the built-in SCIM policies");
    }

    [Fact]
    public void FailsWhenSpacesRegisteredAndAuthoritySetWithWritePolicyNameUsedOnReadSide()
    {
        var sut = new ScimOAuthOptionsValidator(spaces: new FakeSpaceContextAccessor());
        var options = new ScimOAuthOptions
        {
            Authority = "https://example.com",
            ReadAuthorizationPolicyName = "DuendeScimWrite",
            WriteAuthorizationPolicyName = "my-write-policy"
        };

        var result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(
            "ScimOAuthOptions.Authority can't be combined with Spaces and the built-in SCIM policies");
    }

    [Fact]
    public void FailsWhenExplicitBuiltInPolicyNamesSetAndAuthorityBlankOutsideIdentityServer()
    {
        var sut = new ScimOAuthOptionsValidator();
        var options = new ScimOAuthOptions
        {
            Authority = null,
            ReadAuthorizationPolicyName = "DuendeScimRead",
            WriteAuthorizationPolicyName = "DuendeScimWrite"
        };

        var result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Authority must be configured");
    }
}
