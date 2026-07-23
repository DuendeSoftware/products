// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Stores.Storage;
using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.IntegrationTests.Admin.IdentityResources;

/// <summary>
/// Test-only attribute definitions for identity resource extended properties.
/// These are example attributes used exclusively in integration tests.
/// </summary>
internal static class TestIdentityResourceAttributes
{
    public static readonly TypedAttributeDefinition<string> Owner =
        new(AttributeCode.Create("owner"), new ScalarAttributeType(ScalarDataType.String));

    public static readonly TypedAttributeDefinition<int> Version =
        new(AttributeCode.Create("version"), new ScalarAttributeType(ScalarDataType.Integer));

    public static readonly SchemaConfiguration Schema = new()
    {
        SchemaId = SchemaId.IdentityResource,
        DisplayName = "Identity Resource",
        Description = "Extended attributes for identity resources.",
        AttributeDefinitions = [Owner, Version]
    };
}
