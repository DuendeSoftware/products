// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Abstraction for creating a migration test fixture in provider-agnostic tests.
/// Each provider implements this to wire up its own DI and database.
/// </summary>
public interface IMigrationFixtureFactory
{
    /// <summary>
    /// Creates a fresh migration fixture backed by a new (empty) database.
    /// </summary>
    Task<IMigrationFixture> CreateAsync(CancellationToken ct);
}

/// <summary>
/// A disposable fixture that exposes the <see cref="IDatabaseSchema"/> and
/// a way to execute raw SQL against the same database.
/// </summary>
public interface IMigrationFixture : IAsyncDisposable
{
    IDatabaseSchema Schema { get; }

    /// <summary>
    /// Executes raw SQL against the database backing this fixture.
    /// </summary>
    Task ExecuteSqlAsync(string sql, CancellationToken ct);
}
