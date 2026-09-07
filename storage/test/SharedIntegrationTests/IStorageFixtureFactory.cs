// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Abstraction for creating a storage fixture in provider-agnostic integration tests.
/// Each provider implements this to wire up its own DI and database.
/// </summary>
public interface IStorageFixtureFactory
{
    /// <summary>
    /// Creates a fresh storage fixture backed by a new database.
    /// The returned fixture must be disposed to clean up the database.
    /// </summary>
    Task<IStorageFixture> CreateAsync(CancellationToken ct, Action<IServiceCollection>? configure = null);
}

/// <summary>
/// A disposable storage fixture that exposes the <see cref="IStorage"/> under test.
/// </summary>
public interface IStorageFixture : IAsyncDisposable
{
    IStorage Storage { get; }
}
