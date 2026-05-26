// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class UserProfileSelfServicing : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IUserProfileSchemaAdmin _schemaAdmin = null!;
    private IUserProfileSelfService _selfService = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _schemaAdmin = _serviceProvider.GetRequiredService<IUserProfileSchemaAdmin>();
        _selfService = _serviceProvider.GetRequiredService<IUserProfileSelfService>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task Can_register()
    {
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _selfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);

        var user = await _selfService.TryRegisterAsync(UserSubjectId.New(), attributes.Validate(), _ct);

        _ = user.ShouldNotBeNull();
        user.Attributes.Values.ShouldBe(attributes, ignoreOrder: true);
    }

    [Fact]
    public async Task Cannot_register_with_existing_unique_attributes()
    {
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _selfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        _ = (await _selfService.TryRegisterAsync(UserSubjectId.New(), attributes.Validate(), ct: _ct)).ShouldNotBeNull();

        var profile = await _selfService.TryRegisterAsync(UserSubjectId.New(), attributes.Validate(), ct: _ct);

        profile.ShouldBeNull();
    }

    [Fact]
    public async Task Can_get_by_SubjectId()
    {
        var user = (await _selfService.TryRegisterAsync(UserSubjectId.New(), ValidatedAttributeValueCollection.Empty, _ct)).ShouldNotBeNull();

        var actual = await _selfService.TryGetAsync(user.SubjectId, _ct);

        actual.ShouldNotBeNull().SubjectId.ShouldBe(user.SubjectId);
    }

    [Theory]
    [InlineData(typeof(bool))]
    [InlineData(typeof(DateOnly))]
    [InlineData(typeof(DateTimeOffset))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(int))]
    [InlineData(typeof(string))]
    public async Task Can_set_new_attribute(Type type)
    {
        var user = (await _selfService.TryRegisterAsync(UserSubjectId.New(), ValidatedAttributeValueCollection.Empty, _ct)).ShouldNotBeNull();
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _selfService.GetSchemaAsync(_ct);
        var attribute = TestData.CreateAttributes(schema).Single(a => a.UntypedValue.GetType() == type);

        var updatedUser = await TrySetAttribute(user.SubjectId, attribute);

        updatedUser.ShouldNotBeNull().Attributes.Values.ShouldBe([attribute], true);
    }

    [Theory]
    [InlineData(typeof(bool))]
    [InlineData(typeof(DateOnly))]
    [InlineData(typeof(DateTimeOffset))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(int))]
    [InlineData(typeof(string))]
    public async Task Can_set_existing_attribute(Type type)
    {
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _selfService.GetSchemaAsync(_ct);
        var attribute = TestData.CreateAttributes(schema).Single(a => a.UntypedValue.GetType() == type);
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(attribute);
        var user = (await _selfService.TryRegisterAsync(UserSubjectId.New(), attributes.Validate(), _ct)).ShouldNotBeNull();

        var updatedUser = await TrySetAttribute(user.SubjectId, attribute);

        updatedUser.ShouldNotBeNull().Attributes.Values.ShouldHaveSingleItem().ShouldBe(attribute);
    }

    [Fact]
    public async Task Attribute_replacement_removes_previously_existing_attributes()
    {
        // arrange
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _selfService.GetSchemaAsync(_ct);
        var allAttributes = TestData.CreateAttributes(schema);

        var stringAttribute = allAttributes.Single(a => a.UntypedValue is string);
        var intAttribute = allAttributes.Single(a => a.UntypedValue is int);

        var initialAttributes = new AttributeValueCollection(schema);
        initialAttributes.Set(stringAttribute);
        initialAttributes.Set(intAttribute);
        var user = (await _selfService.TryRegisterAsync(UserSubjectId.New(), initialAttributes.Validate(), _ct)).ShouldNotBeNull();

        var initialUser = (await _selfService.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull();
        initialUser.Attributes.Count.ShouldBe(2);
        initialUser.Attributes.Values.ShouldContain(stringAttribute);
        initialUser.Attributes.Values.ShouldContain(intAttribute);

        var currentSchema = await _selfService.GetSchemaAsync(_ct);
        var userUpdate = new AttributeValueCollection(currentSchema, initialUser.Attributes.Values);
        userUpdate.Remove(intAttribute.Code).ShouldBeTrue();

        // act
        var updatedUser = await _selfService.TryUpdateAsync(user.SubjectId, userUpdate.Validate(), _ct);

        // assert
        updatedUser.ShouldNotBeNull().Attributes.Values.ShouldHaveSingleItem().ShouldBe(stringAttribute);
    }

    [Fact]
    public async Task stale_schema_version_returns_null_on_register()
    {
        // Arrange — add a definition so the schema has version 1
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schemaV1 = await _selfService.GetSchemaAsync(_ct);
        var attributesValidatedAgainstV1 = TestData.CreateAttributes(schemaV1).Validate();

        // Advance the schema to version 2 by adding another definition
        (await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = AttributeCode.Create("extra_field"),
                AttributeType = new ScalarAttributeType(ScalarDataType.String),
                Description = AttributeDescription.Create("extra")
            }, _ct)).ShouldBeTrue();

        // Act — try to register with a collection validated against the old schema version
        var result = await _selfService.TryRegisterAsync(UserSubjectId.New(), attributesValidatedAgainstV1, _ct);

        // Assert — staleness check must reject the stale collection
        result.ShouldBeNull();
    }

    [Fact]
    public async Task stale_schema_version_returns_null_on_update()
    {
        // Arrange — register a user with the current schema
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schemaV1 = await _selfService.GetSchemaAsync(_ct);
        var initialAttributes = TestData.CreateAttributes(schemaV1).Validate();
        var user = (await _selfService.TryRegisterAsync(UserSubjectId.New(), initialAttributes, _ct)).ShouldNotBeNull();

        // Capture a validated collection against the current schema version
        var attributesValidatedAgainstV1 = TestData.CreateAttributes(schemaV1).Validate();

        // Advance the schema to a new version
        (await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = AttributeCode.Create("extra_field"),
                AttributeType = new ScalarAttributeType(ScalarDataType.String),
                Description = AttributeDescription.Create("extra")
            }, _ct)).ShouldBeTrue();

        // Act — try to update with a collection validated against the old schema version
        var result = await _selfService.TryUpdateAsync(user.SubjectId, attributesValidatedAgainstV1, _ct);

        // Assert — staleness check must reject the stale collection
        result.ShouldBeNull();
    }

    private async Task<UserProfile?> TrySetAttribute(UserSubjectId subjectId, AttributeValue attribute)
    {
        var user = await _selfService.TryGetAsync(subjectId, _ct);
        if (user is null)
        {
            return null;
        }

        var schema = await _selfService.GetSchemaAsync(_ct);
        var userUpdate = new AttributeValueCollection(schema, user.Attributes.Values);
        userUpdate.Set(attribute);

        return await _selfService.TryUpdateAsync(subjectId, userUpdate.Validate(), _ct);
    }
}
