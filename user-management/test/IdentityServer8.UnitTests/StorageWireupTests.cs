// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

//using Duende.Storage.PostgreSql;
//using Duende.Storage.Sqlite;

namespace IdentityServer8.UnitTests;

public class StorageWireupTests
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    //[Fact]
    //public async Task Can_wire_up_storage_and_postgres()
    //{
    //    var services = new ServiceCollection();

    //    services.AddNpgsqlDataSource("Host=localhost;Port=5432;Database=my_database;Username=postgres;Password=my_password");
    //    var idsrv = services.AddIdentityServer();

    //    // Wire up operational AND configuration stores to storage
    //    idsrv.AddStorage(storage =>
    //    {
    //        storage.AddPostgreSql();
    //    });

    //    idsrv.AddOperationalStorage();

    //    var sp = services.BuildServiceProvider();

    //    var schema = sp.GetRequiredService<IStorageInstanceSchema>();
    //    //await schema.MigrateAsync(Ct);

    //    await sp.GetRequiredService<IApiResourceAdmin>().CreateAsync(new CreateApiResource()
    //    {
    //        Name = "bob"
    //    }, Ct);
    //}

    // [Fact]
    // public async Task Can_wire_up_storage_and_sqlite()
    // {
    //     var services = new ServiceCollection();
    //
    //     var idsrv = services.AddIdentityServer();
    //
    //     // Wire up operational AND configuration stores to storage
    //     idsrv.AddStorage(storage =>
    //     {
    //         storage.AddSqliteInMemory();
    //     });
    //
    //
    //     idsrv.AddConfigurationStorage();
    //     idsrv.AddOperationalStorage();
    //     idsrv.AddUserManagement(c => { });
    //
    //     var sp = services.BuildServiceProvider();
    //
    //     var schema = sp.GetRequiredService<IStorageInstanceSchema>();
    //     await schema.MigrateAsync(Ct);
    //
    //     var result = await sp.GetRequiredService<IApiResourceAdmin>().CreateAsync(new CreateApiResource()
    //     {
    //         Name = "bob"
    //     }, Ct);
    //     result.IsSuccess.ShouldBe(true);
    // }
    //
    // [Fact]
    // public async Task Can_wire_up_storage_and_sqlite_with_explicit_storage_instances()
    // {
    //     var services = new ServiceCollection();
    //
    //     var idsrv = services.AddIdentityServer();
    //
    //     idsrv.AddStorage(_ => { });
    //
    //     // Wire up operational AND configuration stores to storage
    //     idsrv.AddStorage(storage =>
    //     {
    //         storage.AddSqliteInMemory();
    //     });
    //
    //     var differentInstance = StorageInstanceId.Create("different");
    //
    //     idsrv.AddStorage(differentInstance, storage =>
    //     {
    //         storage.AddSqliteInMemory("different");
    //     });
    //
    //     idsrv.AddConfigurationStorage(differentInstance);
    //     idsrv.AddOperationalStorage(differentInstance);
    //
    //     var sp = services.BuildServiceProvider();
    //
    //     var schema = sp.GetRequiredService<IStorageInstanceSchema>();
    //     await schema.MigrateAsync(Ct);
    //
    //     var result = await sp.GetRequiredService<IApiResourceAdmin>().CreateAsync(new CreateApiResource()
    //     {
    //         Name = "bob"
    //     }, Ct);
    //     result.IsSuccess.ShouldBe(true);
    // }
}
