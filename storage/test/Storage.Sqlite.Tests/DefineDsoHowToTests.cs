// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.SearchFields;

namespace Duende.Storage.Sqlite;

public sealed class DefineDsoHowToTests
{
    // begin-snippet: define-dso
    public static class MyDocumentDso
    {
        // Illustrative id only. Real DSOs must use an unused id from the owning product's reserved range.
        internal static readonly EntityType EntityType = new(42, nameof(MyDocumentDso));

        public sealed record V1(string Title, string Content) : IDataStorageObject
        {
            public static DataStorageObjectVersion DsoVersion { get; } = new(EntityType, 1);
        }
    }
    // end-snippet

    [Fact]
    public async Task Can_save_and_retrieve_dso()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await new SqliteStorageEngineFixtureFactory()
            .CreateAsync(ct, services => services.AddDsoRegistration<MyDocumentDso.V1>());
        var storage = fixture.PartitionedStorage;

        var id = UuidV7.New();
        var dso = new MyDocumentDso.V1("My Title", "My Content");

        var createResult = await storage.CreateAsync(id, dso, [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], ct);
        createResult.ShouldBe(CreateResult.Success);

        var result = await storage.TryReadAsync(MyDocumentDso.EntityType, id, ct);

        result.Found.ShouldBeTrue();
        var retrievedDso = (MyDocumentDso.V1)result.Dso;
        retrievedDso.Title.ShouldBe(dso.Title);
        retrievedDso.Content.ShouldBe(dso.Content);
    }
}
