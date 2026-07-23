// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;

namespace Duende.IdentityServer.IntegrationTests.Admin.ApiScopes;

/// <summary>
/// Test-only attribute definitions for API scope extended properties.
/// These are example attributes used exclusively in integration tests.
/// </summary>
internal static class TestApiScopeAttributes
{
    public static readonly TypedAttributeDefinition<string> Owner =
        new(AttributeCode.Create("owner"), new ScalarAttributeType(ScalarDataType.String));

    public static readonly TypedAttributeDefinition<int> Version =
        new(AttributeCode.Create("version"), new ScalarAttributeType(ScalarDataType.Integer));

    public static readonly SchemaConfiguration Schema = new()
    {
        SchemaId = SchemaId.Create("api-scope"),
        DisplayName = "API Scope",
        Description = "Extended attributes for API scopes.",
        AttributeDefinitions = [Owner, Version]
    };
}
