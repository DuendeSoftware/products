// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.PostgreSql;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Duende.Storage;

public sealed class StorageProviderSelectionTests
{
    private static readonly StorageInstanceId Named = StorageInstanceId.Create("named");

    [Fact]
    public void Two_different_providers_in_one_storage_throw()
    {
        var services = new ServiceCollection();

        var ex = Should.Throw<InvalidOperationException>(() => services.AddStorageInternal(Named, storage =>
        {
            _ = storage.AddPostgreSql(_ => NpgsqlDataSource.Create("Host=never-opened"));
            _ = storage.AddSqliteInMemory();
        }));

        ex.Message.ShouldContain("named", customMessage: "the error must name the storage instance that was configured twice");
        ex.Message.ShouldContain("PostgreSQL", customMessage: "the error must name the provider that was selected first");
        ex.Message.ShouldContain("SQLite", customMessage: "the error must name the provider that was selected second");
    }

    [Fact]
    public void The_same_provider_twice_throws()
    {
        var services = new ServiceCollection();

        _ = Should.Throw<InvalidOperationException>(() => services.AddStorageInternal(storage =>
        {
            _ = storage.AddSqliteInMemory();
            _ = storage.AddSqliteInMemory();
        }));
    }

    [Fact]
    public void A_second_storage_registration_for_the_same_instance_throws()
    {
        var services = new ServiceCollection();
        _ = services.AddStorageInternal(storage => storage.AddPostgreSql(_ => NpgsqlDataSource.Create("Host=never-opened")));

        _ = Should.Throw<InvalidOperationException>(() =>
            services.AddStorageInternal(storage => storage.AddSqliteInMemory()));
    }

    [Fact]
    public void Different_instances_can_use_different_providers()
    {
        var services = new ServiceCollection();

        Should.NotThrow(() =>
        {
            _ = services.AddStorageInternal(storage => storage.AddPostgreSql(_ => NpgsqlDataSource.Create("Host=never-opened")));
            _ = services.AddStorageInternal(Named, storage => storage.AddSqliteInMemory());
        });
    }
}
