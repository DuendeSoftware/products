// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Pagination;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Sorting;

namespace Duende.Storage.IntegrationTests;

public static class VersionedContactDso
{
    public static readonly EntityType EntityType = new(101, nameof(VersionedContactDso));

    public sealed record V1(string FullName, string Email) : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
    }

    public sealed record V2(string FirstName, string LastName, string Email) : IDataStorageObject
    {
        public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 2);
    }
}

public partial class QueryStoreDsoVersionTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    private static readonly EntityType Owner = TestDso.DsoVersion.EntityType;

    private static readonly LinkDefinition OwnerContact = new()
    {
        Left = Owner,
        Right = VersionedContactDso.EntityType,
        Link = LinkTypeRegistry.MembershipRole
    };

    private static readonly VersionedContactDso.V1 Ada = new("Ada Lovelace", "ada@example.com");
    private static readonly VersionedContactDso.V2 Grace = new("Grace", "Hopper", "grace@example.com");

    [Fact]
    public async Task when_querying_by_offset_with_rows_of_two_versions_expect_each_row_as_its_stored_version()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;
        _ = await CreateOneContactOfEachVersionAsync(storage);

        var contacts = await storage.QueryAsync(
            VersionedContactDso.EntityType, Query.All(), SortParameter.Empty, DataRange.FromPage(1, 10), _ct);

        contacts.Items.Select(i => (object)i.Value).ShouldBe([Ada, Grace], ignoreOrder: true);
    }

    [Fact]
    public async Task when_querying_by_cursor_with_rows_of_two_versions_expect_each_row_as_its_stored_version()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;
        _ = await CreateOneContactOfEachVersionAsync(storage);

        var contacts = await storage.QueryAsync(
            VersionedContactDso.EntityType, Query.All(), SortParameter.Empty, DataRange.FromContinuationToken(ContinuationToken.Beginning, 10), _ct);

        contacts.Items.Select(i => (object)i.Value).ShouldBe([Ada, Grace], ignoreOrder: true);
    }

    [Fact]
    public async Task when_querying_by_links_with_rows_of_two_versions_expect_each_row_as_its_stored_version()
    {
        await using var fixture = await CreateProviderAsync();
        var storage = fixture.PartitionedStorage;
        var ownerId = await CreateOneContactOfEachVersionAsync(storage);

        var query = LinkQuery.From(VersionedContactDso.EntityType)
            .Join(OwnerContact)
            .Where(Owner, ownerId)
            .Build();

        var contacts = await storage.QueryLinksAsync(query, DataRange.FromPage(1, 10), _ct);

        contacts.Items.Select(i => (object)i.Value).ShouldBe([Ada, Grace], ignoreOrder: true);
    }

    private async Task<IStorageFixture> CreateProviderAsync() =>
        await FixtureFactory.CreateAsync(_ct, services =>
        {
            services.AddDsoRegistration<TestDso>();
            services.AddDsoRegistration<VersionedContactDso.V1>();
            services.AddDsoRegistration<VersionedContactDso.V2>();
        });

    private async Task<UuidV7> CreateOneContactOfEachVersionAsync(IPartitionedStorage storage)
    {
        var ownerId = UuidV7.New();
        var adaId = UuidV7.New();
        var graceId = UuidV7.New();

        (await storage.CreateAsync(ownerId, new TestDso("owner"), [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await storage.CreateAsync(adaId, Ada, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await storage.CreateAsync(graceId, Grace, [], [], Expiration.NoExpiration, [], _ct)).ShouldBe(CreateResult.Success);
        (await storage.LinkAsync(OwnerContact, ownerId, adaId, [], _ct)).ShouldBe(LinkResult.Success);
        (await storage.LinkAsync(OwnerContact, ownerId, graceId, [], _ct)).ShouldBe(LinkResult.Success);

        return ownerId;
    }
}
