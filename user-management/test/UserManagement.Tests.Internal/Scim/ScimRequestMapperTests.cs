// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.UserManagement;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Profiles.Internal;
using Duende.UserManagement.Scim.Internal;
using Duende.UserManagement.Scim.Internal.Endpoints.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimRequestMapperTests : IAsyncLifetime
{
    private ServiceProvider _serviceProvider = null!;
    private IUserProfileSchemaAdmin _schemaAdmin = null!;
    private AttributeSchemaRepository _schemaRepo = null!;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _schemaAdmin = _serviceProvider.GetRequiredService<IUserProfileSchemaAdmin>();
        _schemaRepo = _serviceProvider.GetRequiredService<AttributeSchemaRepository>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    private async Task<AttributeSchema> GetSchemaAsync()
    {
        var result = await _schemaRepo.TryReadAsync(UserProfileSchemaId.Value, _ct);
        _ = result.ShouldNotBeNull();
        return result!.Value.AttributeSchema;
    }

    [Fact]
    public async Task StringAttributeMapsToAttributeValueWithStringValue()
    {
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(new AttributeDefinition
        {
            Code = AttributeCode.Create("nickname"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = AttributeDescription.Create("Nickname")
        }, _ct);
        var schema = await GetSchemaAsync();

        var request = new ScimUserRequest
        {
            Schemas = [ScimConstants.UserSchemaUrn],
            AdditionalAttributes = new Dictionary<string, JsonElement>
            {
                ["nickname"] = JsonDocument.Parse("\"Nicky\"").RootElement
            }
        };

        var result = ScimRequestMapper.Map(request, schema);

        result.IsSuccess.ShouldBeTrue();
        _ = result.Attributes.ShouldNotBeNull();
        result.Attributes.TryGet(AttributeCode.Create("nickname"), out var attr).ShouldBeTrue();
        _ = attr.ShouldNotBeNull();
        attr!.UntypedValue.ShouldBe("Nicky");
    }

    [Fact]
    public async Task BooleanAttributeMapsToAttributeValueWithBoolValue()
    {
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(new AttributeDefinition
        {
            Code = AttributeCode.Create("active"),
            AttributeType = new ScalarAttributeType(ScalarDataType.Boolean),
            Description = AttributeDescription.Create("Active flag")
        }, _ct);
        var schema = await GetSchemaAsync();

        var request = new ScimUserRequest
        {
            Schemas = [ScimConstants.UserSchemaUrn],
            AdditionalAttributes = new Dictionary<string, JsonElement>
            {
                ["active"] = JsonDocument.Parse("true").RootElement
            }
        };

        var result = ScimRequestMapper.Map(request, schema);

        result.IsSuccess.ShouldBeTrue();
        _ = result.Attributes.ShouldNotBeNull();
        result.Attributes.TryGet(AttributeCode.Create("active"), out var attr).ShouldBeTrue();
        _ = attr.ShouldNotBeNull();
        attr!.UntypedValue.ShouldBe(true);
    }

    [Fact]
    public async Task IntegerAttributeMapsToAttributeValueWithIntValue()
    {
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(new AttributeDefinition
        {
            Code = AttributeCode.Create("logincount"),
            AttributeType = new ScalarAttributeType(ScalarDataType.Integer),
            Description = AttributeDescription.Create("Login count")
        }, _ct);
        var schema = await GetSchemaAsync();

        var request = new ScimUserRequest
        {
            Schemas = [ScimConstants.UserSchemaUrn],
            AdditionalAttributes = new Dictionary<string, JsonElement>
            {
                ["logincount"] = JsonDocument.Parse("42").RootElement
            }
        };

        var result = ScimRequestMapper.Map(request, schema);

        result.IsSuccess.ShouldBeTrue();
        _ = result.Attributes.ShouldNotBeNull();
        result.Attributes.TryGet(AttributeCode.Create("logincount"), out var attr).ShouldBeTrue();
        _ = attr.ShouldNotBeNull();
        attr!.UntypedValue.ShouldBe(42);
    }

    [Fact]
    public async Task DecimalAttributeMapsToAttributeValueWithDecimalValue()
    {
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(new AttributeDefinition
        {
            Code = AttributeCode.Create("score"),
            AttributeType = new ScalarAttributeType(ScalarDataType.Decimal),
            Description = AttributeDescription.Create("Score")
        }, _ct);
        var schema = await GetSchemaAsync();

        var request = new ScimUserRequest
        {
            Schemas = [ScimConstants.UserSchemaUrn],
            AdditionalAttributes = new Dictionary<string, JsonElement>
            {
                ["score"] = JsonDocument.Parse("9.5").RootElement
            }
        };

        var result = ScimRequestMapper.Map(request, schema);

        result.IsSuccess.ShouldBeTrue();
        _ = result.Attributes.ShouldNotBeNull();
        result.Attributes.TryGet(AttributeCode.Create("score"), out var attr).ShouldBeTrue();
        _ = attr.ShouldNotBeNull();
        attr!.UntypedValue.ShouldBe(9.5m);
    }

    [Fact]
    public void UnknownAttributeReturnsError()
    {
        var request = new ScimUserRequest
        {
            Schemas = [ScimConstants.UserSchemaUrn],
            AdditionalAttributes = new Dictionary<string, JsonElement>
            {
                ["unknownfield"] = JsonDocument.Parse("\"value\"").RootElement
            }
        };

        var result = ScimRequestMapper.Map(request, null);

        result.IsSuccess.ShouldBeFalse();
        _ = result.ErrorDetail.ShouldNotBeNull();
    }

    [Fact]
    public void InvalidUserNameReturnsError()
    {
        var request = new ScimUserRequest
        {
            Schemas = [ScimConstants.UserSchemaUrn],
            UserName = string.Empty // empty username is invalid
        };

        var result = ScimRequestMapper.Map(request, null);

        result.IsSuccess.ShouldBeFalse();
        _ = result.ErrorDetail.ShouldNotBeNull();
    }

    [Fact]
    public async Task InvalidValueTypeForAttributeReturnsError()
    {
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(new AttributeDefinition
        {
            Code = AttributeCode.Create("age"),
            AttributeType = new ScalarAttributeType(ScalarDataType.Integer),
            Description = AttributeDescription.Create("Age")
        }, _ct);
        var schema = await GetSchemaAsync();

        // Passing a string value for an Integer attribute
        var request = new ScimUserRequest
        {
            Schemas = [ScimConstants.UserSchemaUrn],
            AdditionalAttributes = new Dictionary<string, JsonElement>
            {
                ["age"] = JsonDocument.Parse("\"not-a-number\"").RootElement
            }
        };

        var result = ScimRequestMapper.Map(request, schema);

        result.IsSuccess.ShouldBeFalse();
        _ = result.ErrorDetail.ShouldNotBeNull();
    }

    [Fact]
    public void NullSchemaAllowsUserNameOnlyRequest()
    {
        var request = new ScimUserRequest
        {
            Schemas = [ScimConstants.UserSchemaUrn],
            UserName = "alice"
        };

        var result = ScimRequestMapper.Map(request, null);

        result.IsSuccess.ShouldBeTrue();
        _ = result.UserName.ShouldNotBeNull();
        result.UserName!.Value.ToString().ShouldBe("alice");
    }

    [Fact]
    public void NullSchemaWithExtraAttributesReturnsError()
    {
        var request = new ScimUserRequest
        {
            Schemas = [ScimConstants.UserSchemaUrn],
            AdditionalAttributes = new Dictionary<string, JsonElement>
            {
                ["customfield"] = JsonDocument.Parse("\"value\"").RootElement
            }
        };

        var result = ScimRequestMapper.Map(request, null);

        result.IsSuccess.ShouldBeFalse();
        _ = result.ErrorDetail.ShouldNotBeNull();
    }
}
