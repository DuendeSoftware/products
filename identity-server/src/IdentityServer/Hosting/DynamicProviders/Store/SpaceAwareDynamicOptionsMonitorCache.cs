// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Concurrent;
using Duende.Spaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Hosting.DynamicProviders.Store;

/// <summary>
/// Dynamic-scheme options are held in a separate backing store keyed by <c>(SpaceId, name)</c>,
/// so a bare/static scheme cannot alias them. When no space is configured, the
/// <see cref="SpaceId.Default"/> space is used.
/// </summary>
/// <remarks>
/// A name is routed to the dynamic store only once the scheme has been resolved as dynamic in the
/// current request. <see cref="TryRemove"/> outside a dynamic request context evicts the bare name
/// from every space (so framework options reloads via <see cref="IOptionsMonitor{TOptions}"/>
/// work), as well as the unqualified store. A direct <see cref="IOptionsMonitor{TOptions}"/> read
/// before the scheme is resolved in the request still returns unconfigured options from the
/// unqualified store rather than the space's options.
/// </remarks>
/// <typeparam name="TOptions">The options type.</typeparam>
internal sealed class SpaceAwareDynamicOptionsMonitorCache<TOptions>(
    ISpaceContextAccessor? accessor,
    IHttpContextAccessor httpContextAccessor) : IOptionsMonitorCache<TOptions>, ISpaceAwareDynamicOptionsCache
    where TOptions : class
{
    private readonly OptionsCache<TOptions> _unqualified = new();
    private readonly ConcurrentDictionary<(SpaceId Space, string Name), Lazy<TOptions>> _dynamic = new();

    public void Clear()
    {
        _unqualified.Clear();
        _dynamic.Clear();
    }

    public TOptions GetOrAdd(string? name, Func<TOptions> createOptions)
    {
        name ??= Options.DefaultName;
        if (IsDynamic(name))
        {
            return _dynamic.GetOrAdd(CurrentSpaceKey(name), _ => new Lazy<TOptions>(createOptions, LazyThreadSafetyMode.ExecutionAndPublication)).Value;
        }

        return _unqualified.GetOrAdd(name, createOptions);
    }

    public bool TryAdd(string? name, TOptions options)
    {
        name ??= Options.DefaultName;
        if (IsDynamic(name))
        {
            return _dynamic.TryAdd(CurrentSpaceKey(name), new Lazy<TOptions>(options));
        }

        return _unqualified.TryAdd(name, options);
    }

    public bool TryRemove(string? name)
    {
        name ??= Options.DefaultName;
        if (IsDynamic(name))
        {
            return _dynamic.TryRemove(CurrentSpaceKey(name), out _);
        }

        var removedUnqualified = _unqualified.TryRemove(name);
        var removedAnyDynamic = false;
        foreach (var key in _dynamic.Keys)
        {
            if (key.Name == name && _dynamic.TryRemove(key, out _))
            {
                removedAnyDynamic = true;
            }
        }

        return removedUnqualified || removedAnyDynamic;
    }

    public bool TryRemoveDynamic(string name) => _dynamic.TryRemove(CurrentSpaceKey(name), out _);

    private (SpaceId Space, string Name) CurrentSpaceKey(string name) =>
        (accessor?.GetSpaceIdOrDefault() ?? SpaceId.Default, name);

    private bool IsDynamic(string name) =>
        httpContextAccessor.HttpContext?.RequestServices.GetService<DynamicAuthenticationSchemeCache>()?.Get(name) is not null;
}
