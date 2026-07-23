// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Stores.Storage;
using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.IntegrationTests.Admin.Clients;

/// <summary>
/// Test-only attribute definitions for client extended properties.
/// These are example attributes used exclusively in integration tests.
/// </summary>
internal static class TestClientAttributes
{
    public static readonly TypedAttributeDefinition<string> Department =
        new(AttributeCode.Create("department"), new ScalarAttributeType(ScalarDataType.String));

    public static readonly TypedAttributeDefinition<int> CostCenter =
        new(AttributeCode.Create("cost_center"), new ScalarAttributeType(ScalarDataType.Integer));

    public static readonly TypedAttributeDefinition<string> Environment =
        new(AttributeCode.Create("environment"), new ScalarAttributeType(ScalarDataType.String));

    public static readonly SchemaConfiguration Schema = new()
    {
        SchemaId = SchemaId.Client,
        DisplayName = "Client",
        Description = "Extended attributes for OAuth/OIDC clients.",
        AttributeDefinitions = [Department, CostCenter, Environment]
    };
}
