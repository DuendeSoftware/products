// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer;
using Duende.Storage;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer.UnitTests.Configuration.DependencyInjection;

public class IdentityServerStorageBuilderValidationTests
{
    private static IIdentityServerBuilder CreateBuilder()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services.AddIdentityServer();
    }

    [Fact]
    public void AddStorage_with_two_providers_for_the_same_instance_throws()
    {
        var builder = CreateBuilder();

        Should.Throw<InvalidOperationException>(() => builder.AddStorage(s =>
        {
            s.AddSqliteInMemory();
            s.AddSqliteInMemory();
        }));
    }

    [Fact]
    public void AddStorage_for_an_instance_that_already_has_a_provider_throws()
    {
        var builder = CreateBuilder();
        var operationalStorageInstanceId = StorageInstanceId.Create("operational");
        builder.AddStorage(operationalStorageInstanceId, s => s.AddSqliteInMemory());

        Should.Throw<InvalidOperationException>(() =>
            builder.AddStorage(operationalStorageInstanceId, s => s.AddSqliteInMemory()));
    }

    [Fact]
    public void AddStorage_with_one_provider_per_instance_succeeds()
    {
        var builder = CreateBuilder();

        Should.NotThrow(() => builder
            .AddStorage(StorageInstanceId.Default, s => s.AddSqliteInMemory())
            .AddStorage(StorageInstanceId.Create("operational"), s => s.AddSqliteInMemory()));
    }

    [Fact]
    public void AddOperationalStorage_mapping_the_category_to_a_second_instance_throws()
    {
        var builder = CreateBuilder();
        var defaultStorageInstanceId = StorageInstanceId.Default;
        var otherStorageInstanceId = StorageInstanceId.Create("other");

        builder.AddStorage(defaultStorageInstanceId, s => s.AddSqliteInMemory());
        builder.AddStorage(otherStorageInstanceId, s => s.AddSqliteInMemory());
        builder.AddOperationalStorage(defaultStorageInstanceId);

        Should.Throw<InvalidOperationException>(() => builder.AddOperationalStorage(otherStorageInstanceId));
    }

    [Fact]
    public void AddConfigurationStorage_mapping_the_category_to_a_second_instance_throws()
    {
        var builder = CreateBuilder();
        var defaultStorageInstanceId = StorageInstanceId.Default;
        var otherStorageInstanceId = StorageInstanceId.Create("other");

        builder.AddStorage(defaultStorageInstanceId, s => s.AddSqliteInMemory());
        builder.AddStorage(otherStorageInstanceId, s => s.AddSqliteInMemory());
        builder.AddConfigurationStorage(defaultStorageInstanceId);

        Should.Throw<InvalidOperationException>(() => builder.AddConfigurationStorage(otherStorageInstanceId));
    }
}

