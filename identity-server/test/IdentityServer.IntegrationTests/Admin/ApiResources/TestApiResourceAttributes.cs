// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Stores.Storage;
using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.IntegrationTests.Admin.ApiResources;

/// <summary>
/// Test-only attribute definitions for API resource extended properties.
/// These are example attributes used exclusively in integration tests.
/// </summary>
internal static class TestApiResourceAttributes
{
    public static readonly TypedAttributeDefinition<string> Owner =
        new(AttributeCode.Create("owner"), new ScalarAttributeType(ScalarDataType.String));

    public static readonly TypedAttributeDefinition<int> Version =
        new(AttributeCode.Create("version"), new ScalarAttributeType(ScalarDataType.Integer));

    public static readonly SchemaConfiguration Schema = new()
    {
        SchemaId = SchemaId.ApiResource,
        DisplayName = "API Resource",
        Description = "Extended attributes for API resources.",
        AttributeDefinitions = [Owner, Version]
    };
}
