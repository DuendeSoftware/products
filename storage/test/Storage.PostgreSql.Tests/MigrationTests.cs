// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.PostgreSql;

namespace Duende.Storage.IntegrationTests;

[Collection("PostgreSqlIntegration")]
public partial class MigrationTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new PostgreSqlMigrationFixtureFactory(fixture);
}
