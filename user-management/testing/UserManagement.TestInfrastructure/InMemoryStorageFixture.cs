// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement;

internal sealed class InMemoryStorageFixture
{
    /// <summary>
    /// Gets the DSO type registry used by the fixture.
    /// Register DSO types on this instance before calling <see cref="Build"/>.
    /// </summary>
    public FakeDsoTypeRegistry DsoTypeRegistry { get; } = new();

    /// <summary>
    /// Gets the time provider used by the fixture.
    /// Defaults to <see cref="TimeProvider.System"/>.
    /// </summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>
    /// Creates a new fixture with a default space and system time provider.
    /// </summary>
    public InMemoryStorageFixture()
        : this(TimeProvider.System)
    {
    }

    /// <summary>
    /// Creates a new fixture with custom dependencies.
    /// </summary>
    public InMemoryStorageFixture(TimeProvider timeProvider) => TimeProvider = timeProvider;

    public static IStorage Build()
    {
        var dbId = Guid.NewGuid();
        return new ServiceCollection()
                .AddStorageInternal(storage => storage.AddSqliteStore(opt => opt.ConnectionString = $"Data Source=MySharedDb_{dbId};Mode=Memory;Cache=Shared"))
                .BuildServiceProvider()
                .GetRequiredService<IStorage>();
    }
}
