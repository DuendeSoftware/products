// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.IntegrationTests;

/// <summary>
/// Abstraction for creating a store fixture in provider-agnostic integration tests.
/// Each provider implements this to wire up its own DI and database.
/// </summary>
public interface IStoreFixtureFactory
{
    /// <summary>
    /// Creates a fresh store fixture backed by a new database.
    /// The returned fixture must be disposed to clean up the database.
    /// </summary>
    Task<IStoreFixture> CreateAsync(CancellationToken ct, Action<IServiceCollection>? configure = null);
}

/// <summary>
/// A disposable store fixture that exposes the <see cref="IStore"/> under test.
/// </summary>
public interface IStoreFixture : IAsyncDisposable
{
    IStore Store { get; }
}
