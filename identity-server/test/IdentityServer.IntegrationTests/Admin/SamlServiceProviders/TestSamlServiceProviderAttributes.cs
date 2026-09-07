// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.IntegrationTests.Admin.SamlServiceProviders;

/// <summary>
/// Test-only attribute definitions for SAML service provider extended properties.
/// </summary>
internal static class TestSamlServiceProviderAttributes
{
    public static readonly TypedAttributeDefinition<string> Environment =
        new(AttributeCode.Create("environment"), new ScalarAttributeType(ScalarDataType.String));

    public static readonly TypedAttributeDefinition<int> Priority =
        new(AttributeCode.Create("priority"), new ScalarAttributeType(ScalarDataType.Integer));

    public static readonly SchemaConfiguration Schema = new()
    {
        SchemaId = SchemaId.Create("saml-service-provider"),
        DisplayName = "SAML Service Provider",
        Description = "Extended attributes for SAML service providers.",
        AttributeDefinitions = [Environment, Priority]
    };
}
