// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Scim;

namespace Duende.Platform.UserManagement.Scim;

public sealed class DefaultScimUserSchemaTests
{
    [Fact]
    public async Task schema_can_be_used_by_in_memory_store()
    {
        var configuration = new SchemaConfiguration
        {
            SchemaId = SchemaId.UserProfile,
            Groups = [.. DefaultScimUserSchema.Groups],
            AttributeDefinitions = [.. DefaultScimUserSchema.AttributeDefinitions]
        };
        var store = new InMemorySchemaStore([configuration]);

        var schema = await store.GetAsync(SchemaId.UserProfile, TestContext.Current.CancellationToken);

        schema.Groups.Count.ShouldBe(6);
        schema.AttributeDefinitions.Keys.Select(code => code.Value).ShouldBe(
        [
            "userName",
            "name",
            "displayName",
            "nickName",
            "profileUrl",
            "title",
            "userType",
            "preferredLanguage",
            "locale",
            "timezone",
            "active",
            "emails",
            "phoneNumbers",
            "ims",
            "photos",
            "addresses",
            "entitlements",
            "roles",
            "x509Certificates"
        ], ignoreOrder: true);
    }

    [Fact]
    public void public_members_expose_the_same_attribute_definitions()
    {
        DefaultScimUserSchema.UserName.Code.Value.ShouldBe("userName");
        DefaultScimUserSchema.Emails.Code.Value.ShouldBe("emails");
        DefaultScimUserSchema.X509Certificates.Code.Value.ShouldBe("x509Certificates");
        DefaultScimUserSchema.AttributeDefinitions.ShouldContain(DefaultScimUserSchema.UserName);
        DefaultScimUserSchema.AttributeDefinitions.ShouldContain(DefaultScimUserSchema.X509Certificates);
    }

    [Fact]
    public void schema_marks_only_common_lookup_attributes_as_queryable()
    {
        var definitions = DefaultScimUserSchema.AttributeDefinitions.ToDictionary(definition => definition.Code.Value);

        definitions["userName"].IsRequired.ShouldBeTrue();
        definitions["userName"].IsUnique.ShouldBeTrue();
        definitions.Where(pair => pair.Value.IsQueryable).Select(pair => pair.Key).ShouldBe(
            ["userName", "displayName", "title", "userType", "active"],
            ignoreOrder: true);
    }

    [Fact]
    public void schema_defines_complex_and_multi_valued_attributes()
    {
        var definitions = DefaultScimUserSchema.AttributeDefinitions.ToDictionary(definition => definition.Code.Value);

        var name = definitions["name"].AttributeType.ShouldBeOfType<ComplexAttributeType>();
        name.Properties.Keys.Select(code => code.Value).ShouldContain("givenName");

        var emails = definitions["emails"].AttributeType.ShouldBeOfType<ListAttributeType>();
        var email = emails.ElementType.ShouldBeOfType<ComplexAttributeType>();
        email.Properties.Keys.Select(code => code.Value).ShouldBe(
            ["value", "display", "type", "primary"],
            ignoreOrder: true);
        email.Properties[AttributeCode.Create("primary")].Type.ShouldBe(
            new ScalarAttributeType(ScalarDataType.Boolean));

        var certificates = definitions["x509Certificates"].AttributeType.ShouldBeOfType<ListAttributeType>();
        var certificate = certificates.ElementType.ShouldBeOfType<ComplexAttributeType>();
        certificate.Properties.Keys.Select(code => code.Value).ShouldBe(
            ["value", "display", "type", "primary"],
            ignoreOrder: true);
    }
}
