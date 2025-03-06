// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services;

namespace UnitTests.Common;

public class MockReplayCache : IReplayCache
{
    public bool Exists { get; set; }

    public Task AddAsync(string purpose, string handle, DateTimeOffset expiration)
    {
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string purpose, string handle)
    {
        return Task.FromResult(Exists);
    }
}
