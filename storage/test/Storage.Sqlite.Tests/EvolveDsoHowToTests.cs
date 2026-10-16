// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Storage.IntegrationTests;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;

namespace Duende.Storage.Sqlite;

public sealed class EvolveDsoHowToTests
{
    // begin-snippet: evolve-dso-v1
    public static class ContactCardDso
    {
        // Illustrative id only. Real DSOs must use an unused id from the owning product's reserved range.
        internal static readonly EntityType EntityType = new(777, nameof(ContactCardDso));

        public sealed record V1(string FullName, string Email) : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
        }
    }
    // end-snippet

    // begin-snippet: evolve-dso-v2
    public static class ContactCardDsoV2
    {
        internal static readonly EntityType EntityType = ContactCardDso.EntityType;

        public sealed record V1(string FullName, string Email) : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
        }

        // Breaking change: FullName is split into FirstName and LastName.
        // V1 stays registered so documents already written as V1 remain readable.
        public sealed record V2(string FirstName, string LastName, string Email) : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 2);
        }
    }
    // end-snippet

    private static SearchFieldCollection BuildSearchFields(string name) =>
        new SearchFieldsBuilder().Add("name", name).Build();

    private static async Task<IStorageFixture> CreateFixtureAsync(Ct ct) =>
        await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services =>
            {
                // begin-snippet: evolve-dso-register-both-versions
                services.AddDsoRegistration<ContactCardDsoV2.V1>();
                services.AddDsoRegistration<ContactCardDsoV2.V2>();
                // end-snippet
            });

    [Fact]
    public async Task Can_save_and_retrieve_dso()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services => services.AddDsoRegistration<ContactCardDso.V1>());
        var storage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var dso = new ContactCardDso.V1("Ada Lovelace", "ada@example.com");

        var createResult = await storage.CreateAsync(id, dso, [], BuildSearchFields(dso.FullName), Expiration.NoExpiration, [], ct);
        createResult.ShouldBe(CreateResult.Success);

        var result = await storage.TryReadAsync(ContactCardDso.EntityType, id, ct);

        result.Found.ShouldBeTrue();
        var retrieved = (ContactCardDso.V1)result.Dso;
        retrieved.FullName.ShouldBe(dso.FullName);
        retrieved.Email.ShouldBe(dso.Email);
    }

    // begin-snippet: evolve-dso-backwards-compatible-added-optional-property
    public sealed record WithOptionalNote(string FullName, string Email, string? Note = null);
    // end-snippet

    [Fact]
    public void Adding_an_optional_property_tolerates_documents_written_before_it_existed()
    {
        // A document written by the original V1 shape (no "Note" property at all).
        var originalJson = JsonSerializer.Serialize(new ContactCardDso.V1("Ada Lovelace", "ada@example.com"));

        // Deserializing that older JSON into a shape with an added optional property doesn't throw:
        // the missing property gets its default.
        var upgraded = JsonSerializer.Deserialize<WithOptionalNote>(originalJson);

        _ = upgraded.ShouldNotBeNull();
        upgraded.FullName.ShouldBe("Ada Lovelace");
        upgraded.Email.ShouldBe("ada@example.com");
        upgraded.Note.ShouldBeNull();
    }

    // begin-snippet: evolve-dso-backwards-compatible-removed-property
    public sealed record WithoutEmail(string FullName);
    // end-snippet

    [Fact]
    public void Removing_a_property_tolerates_documents_written_before_it_was_removed()
    {
        // A document written while "Email" still existed.
        var originalJson = JsonSerializer.Serialize(new ContactCardDso.V1("Ada Lovelace", "ada@example.com"));

        // Deserializing into a shape that no longer declares "Email" doesn't throw:
        // the unknown JSON property is ignored.
        var narrowed = JsonSerializer.Deserialize<WithoutEmail>(originalJson);

        _ = narrowed.ShouldNotBeNull();
        narrowed.FullName.ShouldBe("Ada Lovelace");
    }

    [Fact]
    public void Renaming_a_property_silently_loses_the_old_value_instead_of_failing()
    {
        // A document written under the original property name "FullName".
        var originalJson = JsonSerializer.Serialize(new ContactCardDso.V1("Ada Lovelace", "ada@example.com"));

        // A same-version type that merely renames "FullName" to "Name" does NOT throw:
        // it silently deserializes to the default value instead of raising a hard error.
        // This is what makes a rename a breaking change that requires a new schema version -
        // there's no exception to catch, only a quiet data loss.
        var renamed = JsonSerializer.Deserialize<RenamedShape>(originalJson);

        _ = renamed.ShouldNotBeNull();
        renamed.Name.ShouldBeNull();
        renamed.Email.ShouldBe("ada@example.com");
    }

    public sealed record RenamedShape(string? Name, string Email);

    [Fact]
    public async Task Splitting_a_field_into_two_is_a_breaking_change_that_needs_a_new_version()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var v1 = new ContactCardDsoV2.V1("Ada Lovelace", "ada@example.com");
        _ = await storage.CreateAsync(id, v1, [], BuildSearchFields(v1.FullName), Expiration.NoExpiration, [], ct);

        // A direct read by id returns the type of the version that was actually stored (V1 here),
        // not the latest registered version.
        var readResult = await storage.TryReadAsync(ContactCardDsoV2.EntityType, id, ct);
        readResult.Found.ShouldBeTrue();
        _ = readResult.Dso.ShouldBeOfType<ContactCardDsoV2.V1>();
    }

    private static ContactCardDsoV2.V2 ToV2(ContactCardDsoV2.V1 v1)
    {
        var parts = v1.FullName.Split(' ', 2);
        return new ContactCardDsoV2.V2(parts[0], parts.Length > 1 ? parts[1] : "", v1.Email);
    }

    [Fact]
    public void Converting_v1_to_v2_splits_the_full_name()
    {
        var v1 = new ContactCardDsoV2.V1("Ada Lovelace", "ada@example.com");

        // begin-snippet: evolve-dso-conversion-helper
        var parts = v1.FullName.Split(' ', 2);
        var firstName = parts[0];
        var lastName = parts.Length > 1 ? parts[1] : "";
        var v2 = new ContactCardDsoV2.V2(firstName, lastName, v1.Email);
        // end-snippet

        v2.ShouldBe(new ContactCardDsoV2.V2("Ada", "Lovelace", "ada@example.com"));
        ToV2(v1).ShouldBe(v2);
    }

    [Fact]
    public async Task Reading_an_old_version_upgrades_it_in_memory_without_rewriting_the_stored_document()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var v1 = new ContactCardDsoV2.V1("Ada Lovelace", "ada@example.com");
        _ = await storage.CreateAsync(id, v1, [], BuildSearchFields(v1.FullName), Expiration.NoExpiration, [], ct);

        // begin-snippet: evolve-dso-read-then-upgrade
        var result = await storage.TryReadAsync(ContactCardDsoV2.EntityType, id, ct);
        if (!result.Found)
        {
            throw new InvalidOperationException("Contact card not found.");
        }

        // A direct read returns the stored version's type: V1 for rows written before
        // V2 existed, V2 for rows already upgraded. Domain code detects the returned
        // type and converts older versions before continuing.
        var upgraded = result.Dso switch
        {
            ContactCardDsoV2.V2 v2 => v2,
            ContactCardDsoV2.V1 storedV1 => ToV2(storedV1),
            _ => throw new InvalidOperationException($"Unexpected DSO type {result.Dso.GetType()}."),
        };
        // end-snippet

        upgraded.FirstName.ShouldBe("Ada");
        upgraded.LastName.ShouldBe("Lovelace");
        upgraded.Email.ShouldBe("ada@example.com");

        // The stored document itself is unchanged: reading again by id still comes back as V1.
        var stillStoredAsV1 = await storage.TryReadAsync(ContactCardDsoV2.EntityType, id, ct);
        _ = stillStoredAsV1.Dso.ShouldBeOfType<ContactCardDsoV2.V1>();
    }

    [Fact]
    public async Task Lazy_upconversion_persists_v2_only_when_the_row_is_explicitly_written_back()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var v1 = new ContactCardDsoV2.V1("Ada Lovelace", "ada@example.com");
        _ = await storage.CreateAsync(id, v1, [], BuildSearchFields(v1.FullName), Expiration.NoExpiration, [], ct);

        var readResult = await storage.TryReadAsync(ContactCardDsoV2.EntityType, id, ct);
        var upgraded = ToV2(readResult.Dso.ShouldBeOfType<ContactCardDsoV2.V1>());
        var version = readResult.Version!.Value;

        // begin-snippet: evolve-dso-lazy-upconvert-on-write
        // Rebuild search fields from the upgraded shape: the "name" search field path stays
        // the same across V1 and V2, but its value now comes from the split fields.
        var searchFields = new SearchFieldsBuilder()
            .Add("name", $"{upgraded.FirstName} {upgraded.LastName}")
            .Build();

        var updateResult = await storage.UpdateAsync(
            id,
            upgraded,
            version,
            [],
            searchFields,
            expiration: null,
            outboxEvents: [],
            ct);

        if (updateResult != UpdateResult.Success)
        {
            // Someone else updated the entity concurrently; the caller decides how to retry.
            throw new InvalidOperationException($"Could not persist the upgraded contact card: {updateResult}.");
        }
        // end-snippet

        var afterUpgrade = await storage.TryReadAsync(ContactCardDsoV2.EntityType, id, ct);
        var stored = afterUpgrade.Dso.ShouldBeOfType<ContactCardDsoV2.V2>();
        stored.FirstName.ShouldBe("Ada");
        stored.LastName.ShouldBe("Lovelace");
        // UpdateAsync increments the version on success.
        afterUpgrade.Version!.Value.ShouldBe(version + 1);
    }

    [Fact]
    public async Task Query_deserializes_every_matching_row_as_the_requested_type_regardless_of_stored_version()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await CreateFixtureAsync(ct);
        var storage = fixture.PartitionedStorage;

        // One row is still stored as V1 ...
        var v1Id = UuidV7.New();
        var v1 = new ContactCardDsoV2.V1("Ada Lovelace", "ada@example.com");
        _ = await storage.CreateAsync(v1Id, v1, [], BuildSearchFields(v1.FullName), Expiration.NoExpiration, [], ct);

        // ... the other is already stored as V2.
        var v2Id = UuidV7.New();
        var v2 = new ContactCardDsoV2.V2("Grace", "Hopper", "grace@example.com");
        _ = await storage.CreateAsync(v2Id, v2, [], BuildSearchFields($"{v2.FirstName} {v2.LastName}"), Expiration.NoExpiration, [], ct);

        var page = DataRange.FromPage(1, 10);

        var matchAll = new StringField("name").Contains("");

        // Querying as V2 deserializes BOTH rows using the V2 shape. The V1 row has no
        // "FirstName"/"LastName" JSON properties, so those come back null rather than
        // throwing - the row is silently misread, not filtered out or flagged.
        var asV2 = await storage.QueryAsync<ContactCardDsoV2.V2>(
            ContactCardDsoV2.EntityType, matchAll, SortParameter.Empty, page, ct);

        asV2.Items.Count.ShouldBe(2);
        var v1RowReadAsV2 = asV2.Items.Single(i => i.Id == v1Id.Value).Value;
        v1RowReadAsV2.FirstName.ShouldBeNull();
        v1RowReadAsV2.LastName.ShouldBeNull();
        v1RowReadAsV2.Email.ShouldBe("ada@example.com");
        var v2Row = asV2.Items.Single(i => i.Id == v2Id.Value).Value;
        v2Row.FirstName.ShouldBe("Grace");
        v2Row.LastName.ShouldBe("Hopper");

        // Querying as V1 instead deserializes BOTH rows using the V1 shape: the V2 row has
        // no "FullName" JSON property, so it comes back null - the exact mirror problem.
        var asV1 = await storage.QueryAsync<ContactCardDsoV2.V1>(
            ContactCardDsoV2.EntityType, matchAll, SortParameter.Empty, page, ct);

        asV1.Items.Count.ShouldBe(2);
        var v2RowReadAsV1 = asV1.Items.Single(i => i.Id == v2Id.Value).Value;
        v2RowReadAsV1.FullName.ShouldBeNull();
        v2RowReadAsV1.Email.ShouldBe("grace@example.com");
    }
}
