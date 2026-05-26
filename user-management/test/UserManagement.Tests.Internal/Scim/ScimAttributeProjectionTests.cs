// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Scim.Internal;
using Duende.UserManagement.Scim.Internal.Endpoints.Users;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimAttributeProjectionTests
{
    private static readonly ScimUserResource SampleResource = new()
    {
        Schemas = [ScimConstants.UserSchemaUrn],
        Id = "test-id",
        UserName = "alice",
        ExternalId = "ext-001",
        Meta = new ScimMeta { ResourceType = "User", Location = "https://example.com/scim/Users/test-id" },
        AdditionalAttributes = new Dictionary<string, object?>
        {
            ["email"] = "alice@example.com",
            ["department"] = "Engineering"
        }
    };

    private static readonly ScimUserResource ResourceWithPassword = new()
    {
        Schemas = [ScimConstants.UserSchemaUrn],
        Id = "test-id",
        UserName = "bob",
        Meta = new ScimMeta { ResourceType = "User", Location = "https://example.com/scim/Users/test-id" },
        AdditionalAttributes = new Dictionary<string, object?>
        {
            ["password"] = "secret",
            ["email"] = "bob@example.com"
        }
    };

    [Fact]
    public void NoProjectionReturnsResourceUnchangedExceptPasswordStripped()
    {
        var result = ScimAttributeProjection.Apply(SampleResource, null, null);

        result.Id.ShouldBe("test-id");
        result.UserName.ShouldBe("alice");
        result.ExternalId.ShouldBe("ext-001");
        result.Schemas.ShouldNotBeEmpty();
        _ = result.Meta.ShouldNotBeNull();
        var attrs = result.AdditionalAttributes.ShouldNotBeNull();
        attrs.ShouldContainKey("email");
    }

    [Fact]
    public void IncludeUserNameAttributeReturnsOnlyUserNamePlusRequired()
    {
        var attrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "userName" };
        var result = ScimAttributeProjection.Apply(SampleResource, attrs, null);

        result.Id.ShouldBe("test-id");
        result.Schemas.ShouldNotBeEmpty();
        _ = result.Meta.ShouldNotBeNull();
        result.UserName.ShouldBe("alice");
        result.ExternalId.ShouldBeNull();
        result.AdditionalAttributes.ShouldBeEmpty();
    }

    [Fact]
    public void IncludeEmailAttributeReturnsOnlyEmailPlusRequired()
    {
        var attrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "email" };
        var result = ScimAttributeProjection.Apply(SampleResource, attrs, null);

        result.Id.ShouldBe("test-id");
        result.Schemas.ShouldNotBeEmpty();
        _ = result.Meta.ShouldNotBeNull();
        result.UserName.ShouldBeNull();
        result.ExternalId.ShouldBeNull();
        var additional = result.AdditionalAttributes.ShouldNotBeNull();
        additional.ShouldContainKey("email");
        additional.ShouldNotContainKey("department");
    }

    [Fact]
    public void ExcludeUserNameAttributeRemovesUserName()
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "userName" };
        var result = ScimAttributeProjection.Apply(SampleResource, null, excluded);

        result.UserName.ShouldBeNull();
        result.Id.ShouldBe("test-id");
        result.ExternalId.ShouldBe("ext-001");
        var additional = result.AdditionalAttributes.ShouldNotBeNull();
        additional.ShouldContainKey("email");
    }

    [Fact]
    public void CannotExcludeId()
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "id" };
        var result = ScimAttributeProjection.Apply(SampleResource, null, excluded);

        result.Id.ShouldBe("test-id");
    }

    [Fact]
    public void CannotExcludeSchemas()
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "schemas" };
        var result = ScimAttributeProjection.Apply(SampleResource, null, excluded);

        result.Schemas.ShouldNotBeEmpty();
    }

    [Fact]
    public void CannotExcludeMeta()
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "meta" };
        var result = ScimAttributeProjection.Apply(SampleResource, null, excluded);

        _ = result.Meta.ShouldNotBeNull();
    }

    [Fact]
    public void AttributesTakesPrecedenceOverExcludedAttributes()
    {
        // When both are specified, attributes takes precedence per spec
        var attrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "userName" };
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "email" };
        var result = ScimAttributeProjection.Apply(SampleResource, attrs, excluded);

        // Only userName + required fields should be present
        result.UserName.ShouldBe("alice");
        result.ExternalId.ShouldBeNull();
        result.AdditionalAttributes.ShouldBeEmpty();
    }

    [Fact]
    public void PasswordNeverReturnedWithNoProjection()
    {
        var result = ScimAttributeProjection.Apply(ResourceWithPassword, null, null);

        var additional = result.AdditionalAttributes.ShouldNotBeNull();
        additional.ShouldNotContainKey("password");
        additional.ShouldContainKey("email");
    }

    [Fact]
    public void PasswordNeverReturnedEvenWhenExplicitlyRequested()
    {
        var attrs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "password", "email" };
        var result = ScimAttributeProjection.Apply(ResourceWithPassword, attrs, null);

        var additional = result.AdditionalAttributes.ShouldNotBeNull();
        additional.ShouldNotContainKey("password");
    }

    [Fact]
    public void PasswordNeverReturnedEvenWhenNotInExcludedAttributes()
    {
        // Password not listed in excludedAttributes but still should not appear
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "email" };
        var result = ScimAttributeProjection.Apply(ResourceWithPassword, null, excluded);

        var additional = result.AdditionalAttributes.ShouldNotBeNull();
        additional.ShouldNotContainKey("password");
    }
}
