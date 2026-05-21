// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Sqlite;

namespace Duende.Storage.IntegrationTests;

public partial class MigrationTests
{
    private IMigrationFixtureFactory MigrationFixtureFactory { get; } = new SqliteMigrationFixtureFactory();
}
