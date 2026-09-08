// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Scim.Internal;
using Duende.UserManagement.Scim.Internal.Endpoints.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimRequestMapperTests : IAsyncLifetime
{
    private ServiceProvider _serviceProvider = null!;
    private ISchemaAdmin _schemaAdmin = null!;
    private ISchemaStore _schemaStore = null!;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _schemaAdmin = _serviceProvider.GetRequiredService<ISchemaAdmin>();
        _schemaStore = _serviceProvider.GetRequiredService<ISchemaStore>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    private async Task AddDefinitionAsync(AttributeDefinition definition)
    {
        var getResult = await _schemaAdmin.GetAsync(SchemaId.UserProfile, _ct);
        var schema = getResult.Found ? getResult.Item! : new SchemaConfiguration { SchemaId = SchemaId.UserProfile };
        schema.AttributeDefinitions.Add(definition);
        var saveResult = getResult.Found
            ? await _schemaAdmin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, _ct)
            : await _schemaAdmin.CreateAsync(schema, _ct);
        saveResult.IsSuccess.ShouldBeTrue();
    }

    private Task<IReadOnlyAttributeSchema> GetSchemaAsync() => _schemaStore.GetAsync(SchemaId.UserProfile, _ct);

    [Fact]
    public async Task StringAttributeMapsToAttributeValueWithStringValue()
    {
        await AddDefinitionAsync(new AttributeDefinition
        {
            Code = AttributeCode.Create("nickname"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = AttributeDescription.Create("Nickname")
        });
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
        await AddDefinitionAsync(new AttributeDefinition
        {
            Code = AttributeCode.Create("active"),
            AttributeType = new ScalarAttributeType(ScalarDataType.Boolean),
            Description = AttributeDescription.Create("Active flag")
        });
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
        await AddDefinitionAsync(new AttributeDefinition
        {
            Code = AttributeCode.Create("logincount"),
            AttributeType = new ScalarAttributeType(ScalarDataType.Integer),
            Description = AttributeDescription.Create("Login count")
        });
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
        await AddDefinitionAsync(new AttributeDefinition
        {
            Code = AttributeCode.Create("score"),
            AttributeType = new ScalarAttributeType(ScalarDataType.Decimal),
            Description = AttributeDescription.Create("Score")
        });
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
    public async Task InvalidValueTypeForAttributeReturnsError()
    {
        await AddDefinitionAsync(new AttributeDefinition
        {
            Code = AttributeCode.Create("age"),
            AttributeType = new ScalarAttributeType(ScalarDataType.Integer),
            Description = AttributeDescription.Create("Age")
        });
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
    public void NullSchemaRejectsUserName()
    {
        var request = new ScimUserRequest
        {
            Schemas = [ScimConstants.UserSchemaUrn],
            UserName = "alice"
        };

        var result = ScimRequestMapper.Map(request, null);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorDetail.ShouldBe("userName is not defined in the schema.");
        result.ErrorScimType.ShouldBe(ScimConstants.ErrorTypes.InvalidValue);
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

    [Fact]
    public async Task NonUniqueUserNameAttributeReturnsError()
    {
        await AddDefinitionAsync(new AttributeDefinition
        {
            Code = AttributeCode.Create("username"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = AttributeDescription.Create("User login name"),
            IsUnique = false
        });
        var schema = await GetSchemaAsync();
        var request = new ScimUserRequest { Schemas = [ScimConstants.UserSchemaUrn], UserName = "alice" };

        var result = ScimRequestMapper.Map(request, schema);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorDetail.ShouldBe("userName attribute must be configured as unique.");
        result.ErrorScimType.ShouldBe(ScimConstants.ErrorTypes.InvalidValue);
    }

    [Fact]
    public async Task UserNameNotInSchemaReturnsError()
    {
        await AddDefinitionAsync(new AttributeDefinition
        {
            Code = AttributeCode.Create("nickname"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = AttributeDescription.Create("Nickname")
        });
        var schema = await GetSchemaAsync();
        var request = new ScimUserRequest { Schemas = [ScimConstants.UserSchemaUrn], UserName = "alice" };

        var result = ScimRequestMapper.Map(request, schema);

        result.IsSuccess.ShouldBeFalse();
        result.ErrorDetail.ShouldBe("userName is not defined in the schema.");
        result.ErrorScimType.ShouldBe(ScimConstants.ErrorTypes.InvalidValue);
    }
}
