// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AspNetCore.Authentication.JwtBearer.DPoP;

namespace Duende.AspNetCore.Authentication.JwtBearer.TestFramework;

public class TestReplayCache : IReplayCache
{
    private readonly Dictionary<string, (TimeSpan expiration, DateTime addedAt)> _cache = new();
    private readonly List<(string jtiHash, TimeSpan expiration)> _addCalls = new();
    private readonly List<string> _existsCalls = new();

    // Configuration for test behavior
    public Func<string, bool>? ExistsFunc { get; set; }

    public Task Add(string jtiHash, TimeSpan expiration, CT ct = default)
    {
        _addCalls.Add((jtiHash, expiration));
        _cache[jtiHash] = (expiration, DateTime.UtcNow);
        return Task.CompletedTask;
    }

    public Task<bool> Exists(string jtiHash, CT ct = default)
    {
        _existsCalls.Add(jtiHash);

        if (ExistsFunc != null)
        {
            return Task.FromResult(ExistsFunc(jtiHash));
        }

        return Task.FromResult(_cache.ContainsKey(jtiHash));
    }

    public IReadOnlyList<(string jtiHash, TimeSpan expiration)> AddCalls => _addCalls;

    public void Clear()
    {
        _cache.Clear();
        _addCalls.Clear();
        _existsCalls.Clear();
    }
}
