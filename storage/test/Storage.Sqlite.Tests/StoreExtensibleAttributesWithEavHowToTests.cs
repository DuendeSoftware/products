// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Sqlite;

public sealed class StoreExtensibleAttributesWithEavHowToTests
{
    // begin-snippet: eav-device-dso
    public static class DeviceDso
    {
        // Illustrative id only. Real DSOs must use an unused id from the owning product's reserved range.
        internal static readonly EntityType EntityType = new(6001, nameof(DeviceDso));

        // Internal because AttributeValueDso.V1 (Duende.Storage.Internal.*) isn't public;
        // a real product DSO can be public as long as its members only reference public types.
        internal sealed record V1(
            string Name,
            string SerialNumber,
            // Every customer-defined attribute value lives here, validated or not, searchable or not.
            IReadOnlyList<AttributeValueDso.V1> Attributes) : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
        }
    }
    // end-snippet

    private static readonly AttributeGroupCode HardwareGroup = AttributeGroupCode.Create("hardware");

    private static SchemaConfiguration BuildDeviceSchema() => new()
    {
        SchemaId = SchemaId.Create("device"),
        DisplayName = "Device",
        Groups = [new AttributeGroup(HardwareGroup, AttributeDisplayName.Create("Hardware"), null, Order: 0)],
        AttributeDefinitions =
        [
            // Scalar, required, grouped, and indexed for search.
            new AttributeDefinition
            {
                Code = AttributeCode.Create("firmware_version"),
                AttributeType = new ScalarAttributeType(ScalarDataType.String),
                Description = AttributeDescription.Create("Currently installed firmware version"),
                DisplayName = AttributeDisplayName.Create("Firmware version"),
                GroupCode = HardwareGroup,
                IsRequired = true,
                IsQueryable = true,
            },

            // Scalar, optional, indexed for range queries.
            new AttributeDefinition
            {
                Code = AttributeCode.Create("warranty_months"),
                AttributeType = new ScalarAttributeType(ScalarDataType.Integer),
                Description = AttributeDescription.Create("Warranty length in months"),
                GroupCode = HardwareGroup,
                IsQueryable = true,
            },

            // Scalar, kept out of the search index: still stored in the document, but not
            // filterable, sortable, or projectable.
            new AttributeDefinition
            {
                Code = AttributeCode.Create("retired"),
                AttributeType = new ScalarAttributeType(ScalarDataType.Boolean),
                Description = AttributeDescription.Create("Internal flag; not exposed to search"),
                IsQueryable = false,
            },

            // List of scalars, indexed: one search row per item.
            new AttributeDefinition
            {
                Code = AttributeCode.Create("tags"),
                AttributeType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String)),
                Description = AttributeDescription.Create("Free-form labels for grouping devices"),
                IsQueryable = true,
            },

            // Complex (nested) type with named sub-properties.
            new AttributeDefinition
            {
                Code = AttributeCode.Create("shipping_address"),
                AttributeType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
                {
                    [AttributeCode.Create("street")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                    [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                }),
                Description = AttributeDescription.Create("Address the device was shipped to"),
                IsQueryable = false,
            },

            // Scalar, unique: Storage never checks IsUnique itself, so the repository must turn
            // this into an alternate key on every create and update; see "Enforce unique
            // attributes in the repository" below.
            new AttributeDefinition
            {
                Code = AttributeCode.Create("inventory_tag"),
                AttributeType = new ScalarAttributeType(ScalarDataType.String),
                Description = AttributeDescription.Create("Unique inventory tracking id printed on the device"),
                GroupCode = HardwareGroup,
                IsUnique = true,
                IsQueryable = true,
            },

            // List of non-string scalars, indexed: one search row per item, each needing its own
            // itemIndex so items don't collide on the same (field path, item index) pair.
            new AttributeDefinition
            {
                Code = AttributeCode.Create("maintenance_months"),
                AttributeType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.Integer)),
                Description = AttributeDescription.Create("Scheduled maintenance offsets, in months from commissioning"),
                IsQueryable = true,
            },
        ]
    };

    [Fact]
    public void Schema_definition_exposes_scalar_list_and_complex_types()
    {
        // begin-snippet: eav-define-schema
        var hardwareGroup = AttributeGroupCode.Create("hardware");

        var deviceSchema = new SchemaConfiguration
        {
            SchemaId = SchemaId.Create("device"),
            DisplayName = "Device",
            Groups = [new AttributeGroup(hardwareGroup, AttributeDisplayName.Create("Hardware"), null, Order: 0)],
            AttributeDefinitions =
            [
                // Scalar, required, grouped, and indexed for search.
                new AttributeDefinition
                {
                    Code = AttributeCode.Create("firmware_version"),
                    AttributeType = new ScalarAttributeType(ScalarDataType.String),
                    Description = AttributeDescription.Create("Currently installed firmware version"),
                    DisplayName = AttributeDisplayName.Create("Firmware version"),
                    GroupCode = hardwareGroup,
                    IsRequired = true,
                    IsQueryable = true,
                },

                // Scalar, optional, indexed for range queries.
                new AttributeDefinition
                {
                    Code = AttributeCode.Create("warranty_months"),
                    AttributeType = new ScalarAttributeType(ScalarDataType.Integer),
                    Description = AttributeDescription.Create("Warranty length in months"),
                    GroupCode = hardwareGroup,
                    IsQueryable = true,
                },

                // Scalar, kept out of the search index: still stored in the document, but not
                // filterable, sortable, or projectable.
                new AttributeDefinition
                {
                    Code = AttributeCode.Create("retired"),
                    AttributeType = new ScalarAttributeType(ScalarDataType.Boolean),
                    Description = AttributeDescription.Create("Internal flag; not exposed to search"),
                    IsQueryable = false,
                },

                // List of scalars, indexed: one search row per item.
                new AttributeDefinition
                {
                    Code = AttributeCode.Create("tags"),
                    AttributeType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String)),
                    Description = AttributeDescription.Create("Free-form labels for grouping devices"),
                    IsQueryable = true,
                },

                // Complex (nested) type with named sub-properties.
                new AttributeDefinition
                {
                    Code = AttributeCode.Create("shipping_address"),
                    AttributeType = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
                    {
                        [AttributeCode.Create("street")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                        [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                    }),
                    Description = AttributeDescription.Create("Address the device was shipped to"),
                    IsQueryable = false,
                },

                // Scalar, unique: Storage never checks IsUnique itself, so the repository must turn
                // this into an alternate key on every create and update; see "Enforce unique
                // attributes in the repository" below.
                new AttributeDefinition
                {
                    Code = AttributeCode.Create("inventory_tag"),
                    AttributeType = new ScalarAttributeType(ScalarDataType.String),
                    Description = AttributeDescription.Create("Unique inventory tracking id printed on the device"),
                    GroupCode = hardwareGroup,
                    IsUnique = true,
                    IsQueryable = true,
                },

                // List of non-string scalars, indexed: one search row per item, each needing its own
                // itemIndex so items don't collide on the same (field path, item index) pair.
                new AttributeDefinition
                {
                    Code = AttributeCode.Create("maintenance_months"),
                    AttributeType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.Integer)),
                    Description = AttributeDescription.Create("Scheduled maintenance offsets, in months from commissioning"),
                    IsQueryable = true,
                },
            ]
        };
        // end-snippet

        deviceSchema.AttributeDefinitions.Count.ShouldBe(7);
        var firmwareVersion = deviceSchema.AttributeDefinitions.Single(d => d.Code.Value == "firmware_version");
        _ = firmwareVersion.AttributeType.ShouldBeOfType<ScalarAttributeType>();
        firmwareVersion.IsRequired.ShouldBeTrue();
        firmwareVersion.GroupCode.ShouldBe(hardwareGroup);

        var tags = deviceSchema.AttributeDefinitions.Single(d => d.Code.Value == "tags");
        _ = tags.AttributeType.ShouldBeOfType<ListAttributeType>();

        var address = deviceSchema.AttributeDefinitions.Single(d => d.Code.Value == "shipping_address");
        _ = address.AttributeType.ShouldBeOfType<ComplexAttributeType>();
        address.IsQueryable.ShouldBeFalse();
    }


    private static async Task<IStorageFixture> CreateFixtureAsync(Ct ct) =>
        await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services =>
            {
                services.AddDsoRegistration<DeviceDso.V1>();
                services.TryAddSchema(BuildDeviceSchema());
            });

    [Fact]
    public async Task Registering_the_schema_as_a_product_default_makes_it_resolvable()
    {
        var ct = TestContext.Current.CancellationToken;
        var deviceSchema = BuildDeviceSchema();

        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services =>
            {
                // begin-snippet: eav-register-in-memory-schema-store
                services.AddDsoRegistration<DeviceDso.V1>();

                // Registers the schema as a product default: AddStorage's default in-memory
                // ISchemaStore composes every SchemaConfiguration singleton registered this
                // way. TryAddSchema skips registration if a schema with the same SchemaId is
                // already registered, so a customer's own "device" schema always wins; see
                // "Extending a product's schema" below. Use ISchemaAdmin (AddDynamicSchemaStorage)
                // instead for schemas product admins can edit at runtime; see "Schema stores" below.
                services.TryAddSchema(deviceSchema);
                // end-snippet
            });

        var schema = await GetDeviceSchemaAsync(fixture.ServiceProvider, ct);
        schema.AttributeDefinitions.ContainsKey(AttributeCode.Create("firmware_version")).ShouldBeTrue();
    }

    private static async Task<IReadOnlyAttributeSchema> GetDeviceSchemaAsync(IServiceProvider services, Ct ct)
    {
        var store = services.GetRequiredService<ISchemaStore>();

        return await store.GetAsync(SchemaId.Create("device"), ct);
    }

    [Fact]
    public async Task A_customers_schema_wins_over_the_product_default_for_the_same_schema_id()
    {
        var ct = TestContext.Current.CancellationToken;
        var deviceSchema = BuildDeviceSchema();

        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services =>
            {
                // begin-snippet: eav-customer-extends-schema
                var customerSchema = deviceSchema.Extend(new AttributeDefinition
                {
                    Code = AttributeCode.Create("asset_tag"),
                    AttributeType = new ScalarAttributeType(ScalarDataType.String),
                    Description = AttributeDescription.Create("Customer's internal asset tracking id"),
                    IsQueryable = true,
                });

                services.AddDsoRegistration<DeviceDso.V1>();

                // A customer registers their extended schema directly, with the same SchemaId
                // as the product default. Because this is a plain AddSingleton (not
                // TryAddSchema), it always wins over the product's TryAddSchema call for
                // "device" below, regardless of which one runs first.
                _ = services.AddSingleton(customerSchema);

                // The product's own registration: skipped because "device" is already registered.
                services.TryAddSchema(deviceSchema);
                // end-snippet
            });

        var schema = await GetDeviceSchemaAsync(fixture.ServiceProvider, ct);

        schema.AttributeDefinitions.ContainsKey(AttributeCode.Create("asset_tag")).ShouldBeTrue();
    }

    private static ValidatedAttributeValueCollection BuildValidatedAttributes(
        IReadOnlyAttributeSchema schema, string inventoryTag)
    {
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(AttributeCode.Create("firmware_version"), "2.4.1");
        attributes.Set(AttributeCode.Create("warranty_months"), 24);
        attributes.Set(AttributeCode.Create("retired"), false);
        attributes.Set(AttributeCode.Create("tags"), (IReadOnlyList<object>)["gateway", "eu-west"]);
        attributes.Set(AttributeCode.Create("shipping_address"), (IReadOnlyDictionary<string, object>)
            new Dictionary<string, object> { ["street"] = "1 Harbor Way", ["city"] = "Rotterdam" });
        attributes.Set(AttributeCode.Create("inventory_tag"), inventoryTag);

        return attributes.Validate();
    }

    [Fact]
    public async Task Valid_values_validate_against_the_schema()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var schema = await GetDeviceSchemaAsync(fixture.ServiceProvider, ct);

        // begin-snippet: eav-validate-values
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(AttributeCode.Create("firmware_version"), "2.4.1");
        attributes.Set(AttributeCode.Create("warranty_months"), 24);
        attributes.Set(AttributeCode.Create("retired"), false);
        attributes.Set(AttributeCode.Create("tags"), (IReadOnlyList<object>)["gateway", "eu-west"]);
        attributes.Set(AttributeCode.Create("shipping_address"), (IReadOnlyDictionary<string, object>)
            new Dictionary<string, object> { ["street"] = "1 Harbor Way", ["city"] = "Rotterdam" });
        attributes.Set(AttributeCode.Create("inventory_tag"), "INV-1001");

        // Validate() throws if a required attribute is missing. It does not re-check types set
        // through Set(), because the schema-backed collection already rejected a mismatched
        // type at the point it was set.
        var validated = attributes.Validate();
        // end-snippet

        validated.Count.ShouldBe(6);
        validated.TryGet(AttributeCode.Create("firmware_version"), out var firmwareVersion).ShouldBeTrue();
        firmwareVersion.TryGetValue<string>(out var value).ShouldBeTrue();
        value.ShouldBe("2.4.1");
    }

    [Fact]
    public async Task Invalid_values_are_rejected_with_errors()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var schema = await GetDeviceSchemaAsync(fixture.ServiceProvider, ct);

        var attributes = new AttributeValueCollection(schema);

        // Setting the wrong CLR type for a string attribute is rejected immediately.
        attributes.TrySet(AttributeCode.Create("firmware_version"), 123, out var typeErrors).ShouldBeFalse();
        _ = typeErrors.ShouldNotBeNull();

        // "firmware_version" is required and still unset: validation reports the missing attribute.
        attributes.Set(AttributeCode.Create("warranty_months"), 12);
        attributes.TryValidate(out var validated, out var missingErrors).ShouldBeFalse();
        validated.ShouldBeNull();
        _ = missingErrors.ShouldNotBeNull();
        missingErrors.ShouldContain(e => e.Contains("firmware_version"));

        // begin-snippet: eav-validate-values-errors
        var otherAttributes = new AttributeValueCollection(schema);

        // Wrong CLR type for a string attribute.
        var wrongType = otherAttributes.TrySet(AttributeCode.Create("firmware_version"), 123, out _);

        // "firmware_version" is required; never setting it leaves it missing at validation time.
        var validates = otherAttributes.TryValidate(out _, out var otherErrors);
        // end-snippet

        wrongType.ShouldBeFalse();
        validates.ShouldBeFalse();
        _ = otherErrors.ShouldNotBeNull();
    }

    private static SearchFieldCollection BuildSearchFields(
        IReadOnlyAttributeSchema schema, ValidatedAttributeValueCollection attributes)
    {
        var builder = new SearchFieldsBuilder();

        foreach (var attribute in attributes)
        {
            if (!schema.AttributeDefinitions.TryGetValue(attribute.Code, out var definition) || !definition.IsQueryable)
            {
                continue;
            }

            AddSearchFieldsForType(builder, attribute.Code.Value, definition.AttributeType, attribute.UntypedValue, itemIndex: null);
        }

        return builder.Build();
    }

    private static void AddSearchFieldsForType(
        SearchFieldsBuilder builder, string fieldPath, AttributeType type, object value, int? itemIndex)
    {
        switch (type)
        {
            case ScalarAttributeType scalar:
                AddScalarSearchField(builder, fieldPath, scalar.DataType, value, itemIndex);
                break;
            case ListAttributeType list when value is IReadOnlyList<object> items:
                for (var i = 0; i < items.Count; i++)
                {
                    AddSearchFieldsForType(builder, fieldPath, list.ElementType, items[i], itemIndex: i);
                }
                break;
            case ComplexAttributeType complex when value is IReadOnlyDictionary<string, object> dict:
                foreach (var (propCode, prop) in complex.Properties)
                {
                    if (dict.TryGetValue(propCode.Value, out var propValue))
                    {
                        AddSearchFieldsForType(builder, $"{fieldPath}.{propCode.Value}", prop.Type, propValue, itemIndex);
                    }
                }
                break;
        }
    }

    private static void AddScalarSearchField(
        SearchFieldsBuilder builder, string fieldPath, ScalarDataType dataType, object value, int? itemIndex)
    {
        switch (dataType)
        {
            case ScalarDataType.String when value is string s:
                _ = itemIndex is { } i1 ? builder.Add(fieldPath, i1, s) : builder.Add(fieldPath, s);
                break;
            case ScalarDataType.Integer when value is int n:
                _ = itemIndex is { } i2 ? builder.Add(fieldPath, i2, (decimal)n) : builder.Add(fieldPath, (decimal)n);
                break;
            case ScalarDataType.Decimal when value is decimal d:
                _ = itemIndex is { } i3 ? builder.Add(fieldPath, i3, d) : builder.Add(fieldPath, d);
                break;
            case ScalarDataType.Boolean when value is bool b:
                _ = itemIndex is { } i4 ? builder.Add(fieldPath, i4, b) : builder.Add(fieldPath, b);
                break;
            case ScalarDataType.Date when value is DateOnly date:
                var dateValue = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                _ = itemIndex is { } i5 ? builder.Add(fieldPath, i5, dateValue) : builder.Add(fieldPath, dateValue);
                break;
            case ScalarDataType.DateTime when value is DateTimeOffset dt:
                _ = itemIndex is { } i6 ? builder.Add(fieldPath, i6, dt) : builder.Add(fieldPath, dt);
                break;
        }
    }

    [Fact]
    public async Task Building_search_fields_only_indexes_queryable_attributes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;
        var schema = await GetDeviceSchemaAsync(fixture.ServiceProvider, ct);
        var attributes = BuildValidatedAttributes(schema, "INV-2001");

        // begin-snippet: eav-search-fields-from-schema
        var builder = new SearchFieldsBuilder();

        foreach (var attribute in attributes)
        {
            // Mirrors the pattern Duende.UserManagement's UserProfileRepository uses: only
            // attributes the schema marks IsQueryable are written as search fields. Everything
            // else stays in the document only, exactly like the hand-rolled dynamic fields in
            // "How to search and query DSOs".
            if (!schema.AttributeDefinitions.TryGetValue(attribute.Code, out var definition) || !definition.IsQueryable)
            {
                continue;
            }

            AddSearchFieldsForType(builder, attribute.Code.Value, definition.AttributeType, attribute.UntypedValue, itemIndex: null);
        }

        var searchFields = builder.Build();
        // end-snippet

        var id = UuidV7.New();
        var dso = new DeviceDso.V1("Gateway-01", "SN-2001", EavMapper.ToDsoList(attributes));
        (await storage.CreateAsync(id, dso, [], searchFields, Expiration.NoExpiration, [], ct)).ShouldBe(CreateResult.Success);

        // "firmware_version" is IsQueryable: searchable.
        var byFirmware = await storage.QueryAsync<DeviceDso.V1>(
            DeviceDso.EntityType, new StringField("firmware_version").Equals("2.4.1"), SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        byFirmware.Items.Count.ShouldBe(1);

        // "retired" is not IsQueryable: never written as a search field.
        var byRetired = await storage.QueryAsync<DeviceDso.V1>(
            DeviceDso.EntityType, new BooleanField("retired").Equals(false), SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        byRetired.Items.Count.ShouldBe(0);
    }

    private static List<DataStorageKey> BuildUniqueKeys(
        IReadOnlyAttributeSchema schema, ValidatedAttributeValueCollection attributes)
    {
        List<DataStorageKey> keys = [];

        foreach (var attribute in attributes)
        {
            if (!schema.AttributeDefinitions.TryGetValue(attribute.Code, out var definition) || !definition.IsUnique)
            {
                continue;
            }

            keys.Add(DataStorageKey.Create(AttributeValueDskV1.Create(attribute)));
        }

        return keys;
    }

    [Fact]
    public async Task Building_unique_keys_only_includes_attributes_marked_unique()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;
        var schema = await GetDeviceSchemaAsync(fixture.ServiceProvider, ct);
        var attributes = BuildValidatedAttributes(schema, "INV-3001");

        // begin-snippet: eav-unique-keys
        List<DataStorageKey> keys = [];

        foreach (var attribute in attributes)
        {
            // Mirrors Duende.UserManagement's UserProfileRepository.GetJsonKeys: only attributes
            // the schema marks IsUnique become alternate keys. Without this, Storage happily
            // stores two devices with the same "inventory_tag"; see "Enforce unique attributes
            // in the repository" below.
            if (!schema.AttributeDefinitions.TryGetValue(attribute.Code, out var definition) || !definition.IsUnique)
            {
                continue;
            }

            keys.Add(DataStorageKey.Create(AttributeValueDskV1.Create(attribute)));
        }
        // end-snippet

        keys.Count.ShouldBe(1);

        var dso = new DeviceDso.V1("Gateway-01", "SN-3001", EavMapper.ToDsoList(attributes));
        var searchFields = BuildSearchFields(schema, attributes);
        var first = await storage.CreateAsync(UuidV7.New(), dso, keys, searchFields, Expiration.NoExpiration, [], ct);
        first.ShouldBe(CreateResult.Success);

        var duplicate = await storage.CreateAsync(UuidV7.New(), dso, keys, searchFields, Expiration.NoExpiration, [], ct);
        duplicate.ShouldBe(CreateResult.KeyConflict);
    }

    private static async Task<(UuidV7 Id, CreateResult Result)> CreateDeviceAsync(
        IPartitionedStorage storage,
        IReadOnlyAttributeSchema schema,
        string name,
        string serialNumber,
        ValidatedAttributeValueCollection attributes,
        Ct ct)
    {
        var dso = new DeviceDso.V1(name, serialNumber, EavMapper.ToDsoList(attributes));
        var searchFields = BuildSearchFields(schema, attributes);
        var keys = BuildUniqueKeys(schema, attributes);

        var id = UuidV7.New();
        var result = await storage.CreateAsync(id, dso, keys, searchFields, Expiration.NoExpiration, [], ct);
        return (id, result);
    }

    [Fact]
    public async Task Can_persist_and_read_back_device_attributes()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;
        var schema = await GetDeviceSchemaAsync(fixture.ServiceProvider, ct);
        var attributes = BuildValidatedAttributes(schema, "INV-1001");

        // begin-snippet: eav-persist-device
        var dso = new DeviceDso.V1("Gateway-01", "SN-1001", EavMapper.ToDsoList(attributes));
        var searchFields = BuildSearchFields(schema, attributes);
        var keys = BuildUniqueKeys(schema, attributes);

        var id = UuidV7.New();
        var createResult = await storage.CreateAsync(id, dso, keys, searchFields, Expiration.NoExpiration, [], ct);
        // end-snippet

        createResult.ShouldBe(CreateResult.Success);

        // begin-snippet: eav-read-back
        var readResult = await storage.TryReadAsync(DeviceDso.EntityType, id, ct);
        if (!readResult.Found)
        {
            throw new InvalidOperationException("Device not found.");
        }

        var readDso = (DeviceDso.V1)readResult.Dso;

        // EavMapper rehydrates each AttributeValueDso.V1 entry back into a typed AttributeValue,
        // resolving CLR types from the schema. Attributes no longer present in the schema
        // (see "Changing a schema later") are silently skipped.
        var readAttributes = EavMapper.ToAttributeValues(readDso.Attributes, schema).ToList();
        // end-snippet

        readDso.Name.ShouldBe("Gateway-01");
        _ = readAttributes.Single(a => a.Code.Value == "firmware_version").TryGetValue<string>(out var firmwareVersion);
        firmwareVersion.ShouldBe("2.4.1");
        _ = readAttributes.Single(a => a.Code.Value == "retired").TryGetValue<bool>(out var retired);
        retired.ShouldBeFalse();
    }

    [Fact]
    public async Task Can_find_a_device_by_an_indexed_eav_attribute()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;
        var schema = await GetDeviceSchemaAsync(fixture.ServiceProvider, ct);
        var attributes = BuildValidatedAttributes(schema, "INV-1002");

        _ = await CreateDeviceAsync(storage, schema, "Gateway-01", "SN-1001", attributes, ct);

        // begin-snippet: eav-query-by-attribute
        var filter = new StringField("firmware_version").Equals("2.4.1");
        var result = await storage.QueryAsync<DeviceDso.V1>(
            DeviceDso.EntityType, filter, SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        // end-snippet

        result.Items.Count.ShouldBe(1);
        result.Items[0].Value.SerialNumber.ShouldBe("SN-1001");

        // A query over "tags" (also marked IsQueryable) works the same way.
        var byTag = await storage.QueryAsync<DeviceDso.V1>(
            DeviceDso.EntityType, new StringArrayField("tags").Contains("gateway"),
            SortParameter.Empty, DataRange.FromPage(1, 10), ct);
        byTag.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Attributes_marked_not_queryable_cannot_be_searched()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;
        var schema = await GetDeviceSchemaAsync(fixture.ServiceProvider, ct);
        var attributes = BuildValidatedAttributes(schema, "INV-1003");

        _ = await CreateDeviceAsync(storage, schema, "Gateway-01", "SN-1001", attributes, ct);

        // "retired" is stored (see Can_persist_and_read_back_device_attributes) but was never
        // marked IsQueryable, so BuildSearchFields never wrote it as a search field: there is no
        // "retired" row to match against, regardless of the document's actual value.
        var byRetired = await storage.QueryAsync<DeviceDso.V1>(
            DeviceDso.EntityType, new BooleanField("retired").Equals(false),
            SortParameter.Empty, DataRange.FromPage(1, 10), ct);

        byRetired.Items.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Creating_a_second_device_with_the_same_unique_attribute_value_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;
        var schema = await GetDeviceSchemaAsync(fixture.ServiceProvider, ct);

        var first = BuildValidatedAttributes(schema, "DUP-TAG");
        var (_, firstResult) = await CreateDeviceAsync(storage, schema, "Gateway-01", "SN-1001", first, ct);
        firstResult.ShouldBe(CreateResult.Success);

        // Same "inventory_tag" value as the first device: BuildUniqueKeys produces a colliding
        // alternate key, and Storage rejects the second create outright.
        var second = BuildValidatedAttributes(schema, "DUP-TAG");
        var (_, secondResult) = await CreateDeviceAsync(storage, schema, "Gateway-02", "SN-1002", second, ct);
        secondResult.ShouldBe(CreateResult.KeyConflict);

        // A different value is unrelated and succeeds normally.
        var third = BuildValidatedAttributes(schema, "DISTINCT-TAG");
        var (_, thirdResult) = await CreateDeviceAsync(storage, schema, "Gateway-03", "SN-1003", third, ct);
        thirdResult.ShouldBe(CreateResult.Success);
    }

    [Fact]
    public async Task Updating_a_device_to_a_unique_value_already_taken_by_another_device_is_rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;
        var schema = await GetDeviceSchemaAsync(fixture.ServiceProvider, ct);

        var takenAttributes = BuildValidatedAttributes(schema, "TAKEN-TAG");
        _ = await CreateDeviceAsync(storage, schema, "Gateway-01", "SN-1001", takenAttributes, ct);

        var ownAttributes = BuildValidatedAttributes(schema, "OWN-TAG");
        var (secondId, secondResult) = await CreateDeviceAsync(storage, schema, "Gateway-02", "SN-1002", ownAttributes, ct);
        secondResult.ShouldBe(CreateResult.Success);

        // Changing the second device's "inventory_tag" to a value the first device already owns
        // must be rejected on update too, the same as on create.
        var conflictingAttributes = BuildValidatedAttributes(schema, "TAKEN-TAG");

        // begin-snippet: eav-update-device
        // Pass the version from the latest read so a concurrent update is detected.
        var current = await storage.TryReadAsync(DeviceDso.EntityType, secondId, ct);

        // The alternate keys must be rebuilt and passed again on every update, exactly like on
        // create: Storage only knows about the keys supplied with this call, not any keys an
        // earlier create or update happened to pass.
        var updatedDso = new DeviceDso.V1("Gateway-02", "SN-1002", EavMapper.ToDsoList(conflictingAttributes));
        var updatedSearchFields = BuildSearchFields(schema, conflictingAttributes);
        var updatedKeys = BuildUniqueKeys(schema, conflictingAttributes);
        var updateResult = await storage.UpdateAsync(
            secondId, updatedDso, current.Version!.Value, updatedKeys, updatedSearchFields, expiration: null, [], ct);
        // end-snippet

        updateResult.ShouldBe(UpdateResult.KeyConflict);
    }

    [Fact]
    public async Task A_list_of_non_string_scalars_indexes_every_item_without_collisions()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;
        var schema = await GetDeviceSchemaAsync(fixture.ServiceProvider, ct);

        var attributes = new AttributeValueCollection(schema);
        attributes.Set(AttributeCode.Create("firmware_version"), "2.4.1");
        attributes.Set(AttributeCode.Create("warranty_months"), 24);
        attributes.Set(AttributeCode.Create("retired"), false);
        attributes.Set(AttributeCode.Create("tags"), (IReadOnlyList<object>)["gateway"]);
        attributes.Set(AttributeCode.Create("shipping_address"), (IReadOnlyDictionary<string, object>)
            new Dictionary<string, object> { ["street"] = "1 Harbor Way", ["city"] = "Rotterdam" });
        attributes.Set(AttributeCode.Create("inventory_tag"), "INV-LIST");
        // Three non-string items: each needs its own itemIndex, or the second and third
        // overwrite the first's search row instead of adding their own.
        attributes.Set(AttributeCode.Create("maintenance_months"), (IReadOnlyList<object>)[6, 12, 24]);
        var validated = attributes.Validate();

        var dso = new DeviceDso.V1("Gateway-List", "SN-LIST", EavMapper.ToDsoList(validated));
        var searchFields = BuildSearchFields(schema, validated);
        var id = UuidV7.New();
        (await storage.CreateAsync(id, dso, [], searchFields, Expiration.NoExpiration, [], ct))
            .ShouldBe(CreateResult.Success);

        var maintenanceMonths = new NumberField("maintenance_months", isMultiValued: true);
        foreach (var expected in new[] { 6m, 12m, 24m })
        {
            var result = await storage.QueryAsync<DeviceDso.V1>(
                DeviceDso.EntityType, maintenanceMonths.Equals(expected), SortParameter.Empty, DataRange.FromPage(1, 10), ct);
            result.Items.Count.ShouldBe(1, $"expected exactly one device with maintenance_months containing {expected}");
            result.Items[0].Value.SerialNumber.ShouldBe("SN-LIST");
        }
    }
}
