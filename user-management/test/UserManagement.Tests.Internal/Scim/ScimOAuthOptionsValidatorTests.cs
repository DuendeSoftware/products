// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Scim;
using Duende.UserManagement.Scim.Internal;

#pragma warning disable duende_experimental

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimOAuthOptionsValidatorTests
{
    private readonly ScimOAuthOptionsValidator _sut = new();

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
}
