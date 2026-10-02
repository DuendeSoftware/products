// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.UserManagement;
using Duende.UserManagement.Import;
using Duende.UserManagement.Internal.Services;
using Duende.UserManagement.Internal.Storage;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Profiles.Internal.Storage;
using Duende.UserManagement.Scim.Internal;
using Duende.UserManagement.Scim.Internal.Endpoints.Users;
using Duende.UserManagement.Scim.Internal.Models;
using Microsoft.Extensions.DependencyInjection;
using Profile = Duende.UserManagement.Profiles.Internal.UserProfile;
using ProfileSubjectKey = Duende.UserManagement.Profiles.Internal.Storage.UserSubjectIdDskV1;
using StoreQuery = Duende.Storage.Internal.Querying.Query;

namespace Duende.Platform.UserManagement;

public sealed class UserProfileSchemaConsistencyTests : IAsyncLifetime
{
    private static readonly AttributeCode UserName = AttributeCode.Create("username");
    private static readonly AttributeCode Department = AttributeCode.Create("department");
    private static readonly AttributeCode Retained = AttributeCode.Create("retained");

    private readonly AttributeSchema _schema = AttributeSchema.Load(
        [Definition(UserName, true), Definition(Department, false)]);
    private readonly IReadOnlyAttributeSchema _nextSchema = AttributeSchema.Load(
        [Definition(UserName, false), Definition(Department, true)]);
    private readonly SwitchingSchemaStore _schemaStore = new();
    private readonly OverwriteConflictResolver _conflictResolver = new();
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private ServiceProvider _services = null!;
    private UserProfileRepository _repository = null!;
    private IPartitionedStorageFactory _partitionedStorageFactory = null!;

    public async ValueTask InitializeAsync()
    {
        _schemaStore.Use(_schema);
        _services = await UsersServiceProviderFactory.CreateUsersBuilderAsync(null, false, services =>
        {
            _ = services.AddSingleton<ISchemaStore>(_schemaStore);
            _ = services.AddSingleton<IUserImportConflictResolver>(_conflictResolver);
            _ = services.AddSingleton<IServerUrls>(new TestServerUrls());
            _ = services.AddScoped<ScimUserCommandProcessor>();
        });
        _repository = _services.GetRequiredService<UserProfileRepository>();
        _partitionedStorageFactory = _services.GetRequiredService<IPartitionedStorageFactory>();
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Theory]
    [InlineData("admin_create")]
    [InlineData("self_service_create")]
    [InlineData("self_service_update")]
    public async Task service_writes_use_one_schema_read_for_keys_and_search_fields(string operation)
    {
        var subjectId = UserSubjectId.New();
        if (operation == "self_service_update")
        {
            _ = await SeedAsync(subjectId, _schema, Attributes(_schema, "before"));
        }
        _schemaStore.Use(_schema, _nextSchema);
        var attributes = Attributes(_schema, "after");

        var profile = operation switch
        {
            "admin_create" => await _services.GetRequiredService<IUserProfileAdmin>().TryAddAsync(subjectId, attributes, _ct),
            "self_service_create" => await _services.GetRequiredService<IUserProfileSelfService>().TryCreateAsync(subjectId, attributes, _ct),
            "self_service_update" => await _services.GetRequiredService<IUserProfileSelfService>().TryUpdateAsync(subjectId, attributes, _ct),
            _ => throw new InvalidOperationException(operation)
        };

        profile.ShouldNotBeNull().SubjectId.ShouldBe(subjectId);
        _schemaStore.ReadCount.ShouldBe(1);
        await AssertIndexesAsync(subjectId, "after");
        if (operation == "self_service_update")
        {
            await AssertAttributeIndexAsync(subjectId, UserName, "before", false);
        }
        _schemaStore.ReadCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("create_batch")]
    [InlineData("create_aspect")]
    [InlineData("update")]
    [InlineData("update_batch")]
    [InlineData("update_aspect")]
    public async Task repository_writes_index_the_profile_schema_without_reading_the_store(string operation)
    {
        var subjectId = UserSubjectId.New();
        var isUpdate = operation.StartsWith("update", StringComparison.Ordinal);
        var profile = isUpdate
            ? await SeedAsync(subjectId, _schema, Attributes(_schema, "before"))
            : new Profile(subjectId, _schema, Attributes(_schema, "after"));
        if (isUpdate)
        {
            profile.ReplaceAttributes(Attributes(_schema, "after"));
        }
        _schemaStore.Use(_nextSchema);
        var partitionedStorage = await _partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, _ct);

        switch (operation)
        {
            case "create":
                (await _repository.CreateAsync(profile, _ct)).ShouldBe(CreateResult.Success);
                break;
            case "create_batch":
                (await partitionedStorage.ExecuteBatchAsync(await _repository.CreateBatchOperationAsync(profile, _ct), [], _ct)).Success.ShouldBeTrue();
                break;
            case "create_aspect":
                var (create, aspectRef) = UserProfileRepository.CreateAspectBatchOperation(profile);
                aspectRef.Id.ShouldBe(profile.Id.Uuid.Value);
                (await partitionedStorage.ExecuteBatchAsync([create], [], _ct)).Success.ShouldBeTrue();
                break;
            case "update":
                (await _repository.UpdateAsync(profile, 1, _ct)).ShouldBe(UpdateResult.Success);
                break;
            case "update_batch":
                (await partitionedStorage.ExecuteBatchAsync(await _repository.UpdateBatchOperationAsync(profile, 1, _ct), [], _ct)).Success.ShouldBeTrue();
                break;
            case "update_aspect":
                (await partitionedStorage.ExecuteBatchAsync([UserProfileRepository.UpdateAspectOnlyBatchOperation(profile, 1)], [], _ct)).Success.ShouldBeTrue();
                break;
            default:
                throw new InvalidOperationException(operation);
        }

        _schemaStore.ReadCount.ShouldBe(0);
        await AssertIndexesAsync(subjectId, "after");
        if (isUpdate)
        {
            await AssertAttributeIndexAsync(subjectId, UserName, "before", false);
        }
        _schemaStore.ReadCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("replace")]
    [InlineData("replace_profileless_root")]
    [InlineData("patch")]
    public async Task scim_writes_validate_and_index_using_the_single_loaded_schema(string operation)
    {
        var subjectId = UserSubjectId.New();
        if (operation is "replace" or "patch")
        {
            _ = await SeedAsync(subjectId, _schema, Attributes(_schema, "before"));
        }
        else if (operation == "replace_profileless_root")
        {
            (await _services.GetRequiredService<UserRepository>().CreateAsync(subjectId, _ct)).ShouldBe(CreateResult.Success);
        }
        _schemaStore.Use(_schema, _nextSchema);
        var processor = _services.GetRequiredService<ScimUserCommandProcessor>();
        var request = new ScimUserRequest
        {
            Schemas = [ScimConstants.UserSchemaUrn],
            UserName = "after",
            AdditionalAttributes = new Dictionary<string, JsonElement>
            {
                [Department.Value] = JsonSerializer.SerializeToElement("after-department")
            }
        };

        var result = operation switch
        {
            "create" => await processor.CreateAsync(request, _ct),
            "replace" or "replace_profileless_root" => await processor.ReplaceAsync(subjectId.Value, request, null, _ct),
            "patch" => await processor.PatchAsync(subjectId.Value, new ScimPatchRequest
            {
                Schemas = [ScimConstants.PatchOpSchemaUrn],
                Operations =
                [
                    new ScimPatchOperation { Op = "replace", Path = "userName", Value = JsonSerializer.SerializeToElement("after") },
                    new ScimPatchOperation { Op = "replace", Path = Department.Value, Value = JsonSerializer.SerializeToElement("after-department") }
                ]
            }, null, _ct),
            _ => throw new InvalidOperationException(operation)
        };

        result.StatusCode.ShouldBe(operation == "create" ? 201 : 200, result.Detail);
        var resource = result.Value.ShouldBeOfType<ScimUserResource>();
        resource.UserName.ShouldBe("after");
        if (operation == "create")
        {
            subjectId = UserSubjectId.Create(resource.Id);
        }
        resource.Id.ShouldBe(subjectId.Value);
        _schemaStore.ReadCount.ShouldBe(1);
        await AssertIndexesAsync(subjectId, "after");
        if (operation is "replace" or "patch")
        {
            await AssertAttributeIndexAsync(subjectId, UserName, "before", false);
        }
        _schemaStore.ReadCount.ShouldBe(1);
    }

    [Fact]
    public async Task import_creation_reuses_the_batch_schema_for_all_profile_indexes()
    {
        var first = UserSubjectId.New();
        var second = UserSubjectId.New();
        _schemaStore.Use(_schema, _nextSchema);

        var result = await _services.GetRequiredService<IUserImporter>().ImportAsync(
            [
                new UserImportRecord { SubjectId = first, ProfileAttributes = Attributes(_schema, "first") },
                new UserImportRecord { SubjectId = second, ProfileAttributes = Attributes(_schema, "second") }
            ], _ct);

        result.Results.Select(item => item.Status).ShouldBe([UserImportStatus.Created, UserImportStatus.Created]);
        _schemaStore.ReadCount.ShouldBe(1);
        await AssertIndexesAsync(first, "first");
        await AssertIndexesAsync(second, "second");
        _schemaStore.ReadCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task import_merge_uses_the_profile_read_schema_instead_of_the_batch_schema_on_each_attempt(bool retry)
    {
        var profileSchema = AttributeSchema.Load(
        [
            .. _schema.AttributeDefinitions.Values,
            Definition(Retained, true) with { IsRequired = true }
        ]);
        var subjectId = UserSubjectId.New();
        var initial = new AttributeValueCollection(profileSchema, Attributes(_schema, "before"));
        initial.Set(Retained, "keep-me");
        var profile = await SeedAsync(subjectId, profileSchema, initial.Validate());
        _conflictResolver.TargetSubjectId = subjectId;

        if (retry)
        {
            _schemaStore.Use(_nextSchema, _nextSchema, profileSchema, _nextSchema);
            _schemaStore.BeforeRead = async count =>
            {
                if (count == 2)
                {
                    // Advance the persisted aspect after the first merge read, forcing its write to retry.
                    var partitionedStorage = await _partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, _ct);
                    var concurrentWrite = UserProfileRepository.UpdateAspectOnlyBatchOperation(profile, 1);
                    (await partitionedStorage.ExecuteBatchAsync([concurrentWrite], [], _ct)).Success.ShouldBeTrue();
                }
            };
        }
        else
        {
            _schemaStore.Use(_nextSchema, profileSchema, _nextSchema);
        }

        var result = await _services.GetRequiredService<IUserImporter>().ImportAsync(
            [new UserImportRecord { SubjectId = subjectId, ProfileAttributes = Attributes(_schema, "after") }], _ct);

        result.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Updated);
        _conflictResolver.CallCount.ShouldBe(1);
        _schemaStore.ReadCount.ShouldBe(retry ? 3 : 2);
        await AssertIndexesAsync(subjectId, "after");
        await AssertAttributeIndexAsync(subjectId, Retained, "keep-me", true);
        await AssertAttributeIndexAsync(subjectId, UserName, "before", false);
        var partitionedStorageAfter = await _partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, _ct);
        var persisted = await partitionedStorageAfter.TryReadAsync(
            UserProfileDso.EntityType, DataStorageKey.Create(ProfileSubjectKey.Create(subjectId)), _ct);
        persisted.Version.ShouldBe(retry ? 3 : 2);
        var storedAttributes = EavMapper.ToAttributeValues(persisted.Dso.ShouldBeOfType<UserProfileDso.V1>().Attributes, profileSchema);
        storedAttributes.Single(attribute => attribute.Code == Retained).UntypedValue.ShouldBe("keep-me");
        _schemaStore.ReadCount.ShouldBe(retry ? 3 : 2);
    }

    [Theory]
    [InlineData("removed", "Attribute 'retained' is not defined in the schema.")]
    [InlineData("type_changed", "Attribute 'retained' type mismatch: expected 'Integer'.")]
    [InlineData("required", "Required attribute 'required' is missing.")]
    public async Task import_merge_schema_mismatch_fails_only_that_record(string change, string expectedError)
    {
        var incomingSchema = AttributeSchema.Load([.. _schema.AttributeDefinitions.Values, Definition(Retained, false)]);
        var profileSchema = change switch
        {
            "removed" => _schema,
            "type_changed" => AttributeSchema.Load(
            [
                .. _schema.AttributeDefinitions.Values,
                Definition(Retained, false) with { AttributeType = new ScalarAttributeType(ScalarDataType.Integer) }
            ]),
            "required" => AttributeSchema.Load(
            [
                .. incomingSchema.AttributeDefinitions.Values,
                Definition(AttributeCode.Create("required"), false) with { IsRequired = true }
            ]),
            _ => throw new InvalidOperationException(change)
        };
        var subjectId = UserSubjectId.New();
        var nextSubjectId = UserSubjectId.New();
        var initial = Attributes(_schema, "before");
        _ = await SeedAsync(subjectId, _schema, initial);
        _conflictResolver.TargetSubjectId = subjectId;
        var incoming = new AttributeValueCollection(incomingSchema, Attributes(_schema, "after"));
        incoming.Set(Retained, "incoming");
        _schemaStore.Use(incomingSchema, profileSchema);

        var result = await _services.GetRequiredService<IUserImporter>().ImportAsync(
            [
                new UserImportRecord { SubjectId = subjectId, ProfileAttributes = incoming.Validate() },
                new UserImportRecord { SubjectId = nextSubjectId, ProfileAttributes = Attributes(incomingSchema, "next") }
            ], _ct);

        result.Results.Count.ShouldBe(2);
        result.Results[0].SubjectId.ShouldBe(subjectId);
        result.Results[0].Status.ShouldBe(UserImportStatus.Failed);
        result.Results[0].Error.ShouldBe(expectedError);
        result.Results[1].SubjectId.ShouldBe(nextSubjectId);
        result.Results[1].Status.ShouldBe(UserImportStatus.Created);
        result.Results[1].Error.ShouldBeNull();
        _schemaStore.ReadCount.ShouldBe(2);
        var partitionedStorage = await _partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, _ct);
        var persisted = await partitionedStorage.TryReadAsync(
            UserProfileDso.EntityType, DataStorageKey.Create(ProfileSubjectKey.Create(subjectId)), _ct);
        persisted.Version.ShouldBe(1);
        var storedAttributes = EavMapper.ToAttributeValues(persisted.Dso.ShouldBeOfType<UserProfileDso.V1>().Attributes, _schema);
        storedAttributes.ShouldBe(initial, ignoreOrder: true);
        await AssertIndexesAsync(subjectId, "before");
        await AssertAttributeIndexAsync(subjectId, UserName, "after", false);
        await AssertIndexesAsync(nextSubjectId, "next");
    }

    private async Task<Profile> SeedAsync(UserSubjectId subjectId, IReadOnlyAttributeSchema schema, ValidatedAttributeValueCollection attributes)
    {
        var profile = new Profile(subjectId, schema, attributes);
        (await _repository.CreateAsync(profile, _ct)).ShouldBe(CreateResult.Success);
        return profile;
    }

    private async Task AssertIndexesAsync(UserSubjectId subjectId, string value)
    {
        await AssertAttributeIndexAsync(subjectId, UserName, value, true);
        await AssertAttributeIndexAsync(subjectId, Department, $"{value}-department", false);
    }

    private async Task AssertAttributeIndexAsync(UserSubjectId subjectId, AttributeCode code, string value, bool indexed)
    {
        // Query partitioned storage directly so verification cannot consume another schema from the sequence.
        var partitionedStorage = await _partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.UserManagement, _ct);
        var keyed = await partitionedStorage.TryReadAsync(
            UserProfileDso.EntityType, DataStorageKey.Create(AttributeValueDskV1.Create(code, value)), _ct);
        keyed.Found.ShouldBe(indexed);
        if (indexed)
        {
            keyed.Dso.ShouldBeOfType<UserProfileDso.V1>().SubjectId.ShouldBe(subjectId.Value);
        }
        var field = new StringField(code.Value);
        var query = await partitionedStorage.QueryFieldsAsync(
            UserProfileDso.EntityType,
            [field],
            StoreQuery.Where(field.Equals(value)),
            SortParameter.Empty,
            DataRange.FromPage(1, 20),
            _ct);
        query.TotalCount.ShouldBe(indexed ? 1 : 0);
    }

    private static AttributeDefinition Definition(AttributeCode code, bool indexed) => new()
    {
        Code = code,
        AttributeType = new ScalarAttributeType(ScalarDataType.String),
        Description = AttributeDescription.Create(code.Value),
        IsUnique = indexed,
        IsQueryable = indexed
    };

    private static ValidatedAttributeValueCollection Attributes(IReadOnlyAttributeSchema schema, string value)
    {
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(UserName, value);
        attributes.Set(Department, $"{value}-department");
        return attributes.Validate();
    }

    private sealed class SwitchingSchemaStore : ISchemaStore
    {
        private IReadOnlyAttributeSchema[] _schemas = [];

        internal int ReadCount { get; private set; }
        internal Func<int, Task> BeforeRead { get; set; } = _ => Task.CompletedTask;

        internal void Use(params IReadOnlyAttributeSchema[] schemas)
        {
            _schemas = schemas;
            ReadCount = 0;
        }

        public async Task<IReadOnlyAttributeSchema> GetAsync(SchemaId schemaId, Ct ct)
        {
            schemaId.ShouldBe(SchemaId.UserProfile);
            var index = ReadCount++;
            await BeforeRead(ReadCount);
            return _schemas[Math.Min(index, _schemas.Length - 1)];
        }
    }

    private sealed class OverwriteConflictResolver : IUserImportConflictResolver
    {
        internal UserSubjectId TargetSubjectId { get; set; } = UserSubjectId.New();
        internal int CallCount { get; private set; }

        public Task<UserImportConflictResolution> ResolveAsync(UserImportConflict conflict, Ct ct)
        {
            CallCount++;
            return Task.FromResult<UserImportConflictResolution>(new UserImportConflictResolution.Overwrite(TargetSubjectId));
        }
    }

    private sealed class TestServerUrls : IServerUrls
    {
        public string Origin { get; set; } = "https://example.com";
        public string? BasePath { get; set; }
    }
}
