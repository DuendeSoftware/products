// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Stores.Storage;
using Duende.IdentityServer.Stores.Storage.IdentityProviders;
using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.IntegrationTests.Admin.IdentityProviders;

internal static class TestIdentityProviderAttributes
{
    public static readonly TypedAttributeDefinition<string> TenantId =
        new(AttributeCode.Create("tenant_id"), new ScalarAttributeType(ScalarDataType.String));

    public static readonly SchemaConfiguration Schema = new()
    {
        SchemaId = SchemaId.IdentityProvider("oidc"),
        DisplayName = "OIDC Identity Provider (test)",
        Description = "Test schema for OIDC identity providers with extended attributes.",
        AttributeDefinitions =
        [
            ..DefaultOidcProviderSchema.Schema.AttributeDefinitions,
            TenantId
        ]
    };
}
