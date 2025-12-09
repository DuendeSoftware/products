// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

public class TestReplayCache : IReplayCache
{
    private readonly Dictionary<string, (TimeSpan expiration, DateTime addedAt)> _cache = new();
    private readonly List<(string jtiHash, TimeSpan expiration)> _addCalls = new();
    private readonly List<string> _existsCalls = new();

    // Configuration for test behavior
    public Func<string, bool>? ExistsFunc { get; set; }

    public Task Add(string jtiHash, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        _addCalls.Add((jtiHash, expiration));
        _cache[jtiHash] = (expiration, DateTime.UtcNow);
        return Task.CompletedTask;
    }

    public Task<bool> Exists(string jtiHash, CancellationToken cancellationToken = default)
    {
        _existsCalls.Add(jtiHash);

        if (ExistsFunc != null)
        {
            return Task.FromResult(ExistsFunc(jtiHash));
        }

        return Task.FromResult(_cache.ContainsKey(jtiHash));
    }

    // Verification methods
    public void VerifyAddWasCalled(string jtiHash, TimeSpan expectedExpiration)
    {
        var call = _addCalls.FirstOrDefault(c => c.jtiHash == jtiHash);
        if (call == default)
        {
            throw new Exception($"Add was not called with jtiHash: {jtiHash}");
        }
        if (call.expiration != expectedExpiration)
        {
            throw new Exception($"Add was called with wrong expiration. Expected: {expectedExpiration}, Actual: {call.expiration}");
        }
    }

    public void VerifyAddWasNotCalled()
    {
        if (_addCalls.Count > 0)
        {
            throw new Exception($"Add was called {_addCalls.Count} time(s) but should not have been called");
        }
    }

    public bool WasAddCalled => _addCalls.Count > 0;

    public IReadOnlyList<(string jtiHash, TimeSpan expiration)> AddCalls => _addCalls;

    public void Clear()
    {
        _cache.Clear();
        _addCalls.Clear();
        _existsCalls.Clear();
    }
}
