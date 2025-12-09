// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP.TestFramework;

public class TestOptionsMonitor<T>(T options) : IOptionsMonitor<T>
{
    private readonly T _options = options;

    public T CurrentValue => _options;

    public T Get(string? name) => _options;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
