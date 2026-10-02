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
    private ISchemaAdmin _schemaAdmin = null!;
    private IUserProfileSelfService _selfService = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _schemaAdmin = _serviceProvider.GetRequiredService<ISchemaAdmin>();
        _selfService = _serviceProvider.GetRequiredService<IUserProfileSelfService>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Fact]
    public async Task Can_register()
    {
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _selfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);

        var user = await _selfService.TryCreateAsync(UserSubjectId.New(), attributes.Validate(), _ct);

        _ = user.ShouldNotBeNull();
        user.Schema.AttributeDefinitions.Keys.ShouldBe(schema.AttributeDefinitions.Keys, ignoreOrder: true);
        user.Attributes.Values.ShouldBe(attributes, ignoreOrder: true);
    }

    [Fact]
    public async Task Cannot_register_with_existing_unique_attributes()
    {
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _selfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        _ = (await _selfService.TryCreateAsync(UserSubjectId.New(), attributes.Validate(), ct: _ct)).ShouldNotBeNull();

        var profile = await _selfService.TryCreateAsync(UserSubjectId.New(), attributes.Validate(), ct: _ct);

        profile.ShouldBeNull();
    }

    [Fact]
    public async Task Can_get_by_SubjectId()
    {
        var user = (await _selfService.TryCreateAsync(UserSubjectId.New(), ValidatedAttributeValueCollection.Empty, _ct)).ShouldNotBeNull();

        var actual = await _selfService.TryGetAsync(user.SubjectId, _ct);

        actual.ShouldNotBeNull().SubjectId.ShouldBe(user.SubjectId);
        actual.Schema.AttributeDefinitions.ShouldBeEmpty();
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
        var user = (await _selfService.TryCreateAsync(UserSubjectId.New(), ValidatedAttributeValueCollection.Empty, _ct)).ShouldNotBeNull();
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
        var user = (await _selfService.TryCreateAsync(UserSubjectId.New(), attributes.Validate(), _ct)).ShouldNotBeNull();

        var updatedUser = await TrySetAttribute(user.SubjectId, attribute);

        updatedUser.ShouldNotBeNull().Attributes.Values.ShouldHaveSingleItem().ShouldBe(attribute);
    }

    [Fact]
    public async Task can_replace_attributes_using_the_profile_schema()
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
        var user = (await _selfService.TryCreateAsync(UserSubjectId.New(), initialAttributes.Validate(), _ct)).ShouldNotBeNull();

        var initialUser = (await _selfService.TryGetAsync(user.SubjectId, _ct)).ShouldNotBeNull();
        initialUser.Attributes.Count.ShouldBe(2);
        initialUser.Attributes.Values.ShouldContain(stringAttribute);
        initialUser.Attributes.Values.ShouldContain(intAttribute);

        var userUpdate = initialUser.ToUpdate();
        userUpdate.Remove(intAttribute.Code).ShouldBeTrue();

        // act
        var updatedUser = await _selfService.TryUpdateAsync(user.SubjectId, userUpdate.Validate(), _ct);

        // assert
        updatedUser.ShouldNotBeNull().Attributes.Values.ShouldHaveSingleItem().ShouldBe(stringAttribute);
    }

    [Fact]
    public async Task ToUpdate_returns_a_copy_independent_of_the_profile()
    {
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _selfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var user = (await _selfService.TryCreateAsync(UserSubjectId.New(), attributes.Validate(), _ct)).ShouldNotBeNull();

        var update = user.ToUpdate();

        update.ShouldBe(user.Attributes.Values, ignoreOrder: true);

        var someAttribute = user.Attributes.Values.First();
        var otherCode = user.Attributes.Values.Select(a => a.Code).First(c => c != someAttribute.Code);

        _ = update.Remove(someAttribute.Code);
        _ = update.Remove(otherCode);

        user.Attributes.Values.ShouldContain(someAttribute);
        user.Attributes.Count.ShouldBe(attributes.Count());
    }

    [Fact]
    public async Task ToUpdate_edits_can_be_validated_and_persisted()
    {
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _selfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var stringAttribute = attributes.Single(a => a.UntypedValue is string);
        var user = (await _selfService.TryCreateAsync(UserSubjectId.New(), attributes.Validate(), _ct)).ShouldNotBeNull();

        var update = user.ToUpdate();
        var replacement = AttributeValue.Load(stringAttribute.Code, $"{stringAttribute.UntypedValue}-edited");
        update.Set(replacement);

        var updatedUser = await _selfService.TryUpdateAsync(user.SubjectId, update.Validate(), _ct);

        updatedUser.ShouldNotBeNull().Attributes.Values.ShouldContain(replacement);
    }

    [Fact]
    public async Task ToUpdate_enforces_schema_when_setting_an_undefined_code()
    {
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _selfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var user = (await _selfService.TryCreateAsync(UserSubjectId.New(), attributes.Validate(), _ct)).ShouldNotBeNull();

        var update = user.ToUpdate();

        _ = Should.Throw<ArgumentException>(() => update.Set(AttributeValue.Load(AttributeCode.Create("undefined_attribute"), "value")));
    }

    [Fact]
    public async Task ToUpdate_enforces_schema_when_setting_the_wrong_type()
    {
        await TestData.AddAttributeDefinitions(_schemaAdmin, _ct);
        var schema = await _selfService.GetSchemaAsync(_ct);
        var attributes = TestData.CreateAttributes(schema);
        var stringAttribute = attributes.Single(a => a.UntypedValue is string);
        var user = (await _selfService.TryCreateAsync(UserSubjectId.New(), attributes.Validate(), _ct)).ShouldNotBeNull();

        var update = user.ToUpdate();

        _ = Should.Throw<ArgumentException>(() => update.Set(AttributeValue.Load(stringAttribute.Code, 123)));
    }

    [Fact]
    public async Task ToUpdate_throws_when_removing_a_required_attribute()
    {
        var getResult = await _schemaAdmin.GetAsync(SchemaId.UserProfile, _ct);
        var schemaConfiguration = getResult.Found
            ? getResult.Item!
            : new SchemaConfiguration { SchemaId = SchemaId.UserProfile };
        var requiredCode = AttributeCode.Create("required_attribute");
        schemaConfiguration.AttributeDefinitions.Add(new AttributeDefinition
        {
            Code = requiredCode,
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = AttributeDescription.Create("required attribute"),
            IsRequired = true
        });
        var saveResult = getResult.Found
            ? await _schemaAdmin.UpdateAsync(SchemaId.UserProfile, schemaConfiguration, getResult.Version!.Value, _ct)
            : await _schemaAdmin.CreateAsync(schemaConfiguration, _ct);
        saveResult.IsSuccess.ShouldBeTrue();

        var schema = await _selfService.GetSchemaAsync(_ct);
        var requiredAttribute = AttributeValue.Load(requiredCode, "required-value");
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(requiredAttribute);
        var user = (await _selfService.TryCreateAsync(UserSubjectId.New(), attributes.Validate(), _ct)).ShouldNotBeNull();

        var update = user.ToUpdate();

        _ = Should.Throw<InvalidOperationException>(() => update.Remove(requiredCode));
    }

    private async Task<UserProfile?> TrySetAttribute(UserSubjectId subjectId, AttributeValue attribute)
    {
        var user = await _selfService.TryGetAsync(subjectId, _ct);
        if (user is null)
        {
            return null;
        }

        var userUpdate = user.ToUpdate();
        userUpdate.Set(attribute);

        return await _selfService.TryUpdateAsync(subjectId, userUpdate.Validate(), _ct);
    }
}
