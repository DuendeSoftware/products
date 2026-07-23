// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Oracle;

namespace Duende.Storage.IntegrationTests;

[Collection("OracleIntegration")]
public partial class MigrationTests(AspireFixture fixture)
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new OracleMigrationFixtureFactory(fixture);
}
