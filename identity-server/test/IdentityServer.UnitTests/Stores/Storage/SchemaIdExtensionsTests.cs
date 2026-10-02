// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Stores.Storage;
using Duende.Storage.EntityAttributeValue;

namespace UnitTests.Stores.Storage;

public class SchemaIdExtensionsTests
{
    [Fact]
    public void oidc_identity_provider_has_expected_value() =>
        SchemaId.OidcIdentityProvider.Value.ShouldBe("idp:oidc");

    [Fact]
    public void saml_identity_provider_has_expected_value() =>
        SchemaId.SamlIdentityProvider.Value.ShouldBe("idp:saml");

    [Fact]
    public void build_identity_provider_id_prefixes_provider_type()
    {
        var schemaId = SchemaId.BuildIdentityProviderId("custom");

        schemaId.Value.ShouldBe("idp:custom");
    }

    [Fact]
    public void build_identity_provider_id_throws_for_null_type()
    {
        var exception = Should.Throw<ArgumentNullException>(() => SchemaId.BuildIdentityProviderId(null!));

        exception.ParamName.ShouldBe("type");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void build_identity_provider_id_throws_for_empty_or_whitespace_type(string type)
    {
        var exception = Should.Throw<ArgumentException>(() => SchemaId.BuildIdentityProviderId(type));

        exception.ParamName.ShouldBe("type");
    }
}
