// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;

namespace Shouldly;

[ShouldlyMethods]
[DebuggerStepThrough]
public static class TestReplayCacheShouldlyExtensions
{
    /// <summary>
    /// Asserts that Add was called on the replay cache with the specified jtiHash and expiration.
    /// </summary>
    public static void ShouldHaveAdded(
        this Duende.AspNetCore.Authentication.JwtBearer.DPoP.TestReplayCache cache,
        string jtiHash,
        TimeSpan expectedExpiration,
        string? customMessage = null)
    {
        var call = cache.AddCalls.FirstOrDefault(c => c.jtiHash == jtiHash);

        if (call == default)
        {
            var errorMessage = customMessage ??
                $"Expected Add to be called with jtiHash '{jtiHash}', but it was not called with that value.\n" +
                $"Actual calls: {(cache.AddCalls.Count == 0 ? "none" : string.Join(", ", cache.AddCalls.Select(c => $"'{c.jtiHash}'")))}";
            throw new ShouldAssertException(errorMessage);
        }

        if (call.expiration != expectedExpiration)
        {
            var errorMessage = customMessage ??
                $"Expected Add to be called with expiration {expectedExpiration}, but was called with {call.expiration}.";
            throw new ShouldAssertException(errorMessage);
        }
    }

    /// <summary>
    /// Asserts that Add was not called on the replay cache.
    /// </summary>
    public static void ShouldNotHaveBeenAdded(
        this Duende.AspNetCore.Authentication.JwtBearer.DPoP.TestReplayCache cache,
        string? customMessage = null)
    {
        if (cache.AddCalls.Count > 0)
        {
            var errorMessage = customMessage ??
                $"Expected Add not to be called, but it was called {cache.AddCalls.Count} time(s).\n" +
                $"Calls: {string.Join(", ", cache.AddCalls.Select(c => $"jtiHash='{c.jtiHash}', expiration={c.expiration}"))}";
            throw new ShouldAssertException(errorMessage);
        }
    }
}
