// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.CliPlugin.Commands;

public sealed class DatabaseProviderFactoryTests
{
    [Theory]
    [InlineData("postgresql")]
    [InlineData("mssql")]
    [InlineData("sqlite")]
    public async Task Factory_resolves_database_schema_for_provider(string provider)
    {
        var connectionString = provider == "sqlite"
            ? $"Data Source=TestDb{Guid.NewGuid():N};Mode=Memory;Cache=Shared"
            : "Host=localhost;Database=test";

        await using var serviceProvider = DatabaseProviderFactory.CreateServiceProvider(provider, connectionString, null);

        var schema = serviceProvider.GetRequiredService<IDatabaseSchema>();
        _ = schema.ShouldNotBeNull();
    }

    [Fact]
    public void Invalid_provider_throws_ArgumentException() =>
        _ = Should.Throw<ArgumentException>(() => DatabaseProviderFactory.CreateServiceProvider("invalid", "connStr", null));
}
