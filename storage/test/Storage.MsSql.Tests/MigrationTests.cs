// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.MsSql;

namespace Duende.Storage.IntegrationTests;

[Collection("MsSqlIntegration")]
public partial class MigrationTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new MsSqlMigrationFixtureFactory(fixture);
}
