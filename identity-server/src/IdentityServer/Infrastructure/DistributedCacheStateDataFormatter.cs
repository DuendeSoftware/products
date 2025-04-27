// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Infrastructure;

/// <summary>
/// State formatter using IDistributedCache
/// </summary>
public class DistributedCacheStateDataFormatter : ISecureDataFormat<AuthenticationProperties>
{
    private readonly string _name;
    private readonly IServiceProvider _provider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCacheStateDataFormatter"/> class.
    /// </summary>
    /// <param name="provider">The service provider.</param>
    /// <param name="name">The scheme name.</param>
    public DistributedCacheStateDataFormatter(IServiceProvider provider, string name)
    {
        _provider = provider;
        _name = name;
    }

    private string CacheKeyPrefix => "DistributedCacheStateDataFormatter";

    private IDistributedCache Cache => _provider.GetRequiredService<IDistributedCache>();
    private IDataProtector Protector => _provider.GetRequiredService<IDataProtectionProvider>().CreateProtector(CacheKeyPrefix, _name);

    /// <summary>
    /// Protects the specified data.
    /// </summary>
    /// <param name="data">The data.</param>
    public string Protect(AuthenticationProperties data) => Protect(data, null);

    /// <summary>
    /// Protects the specified data.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="purpose">The purpose.</param>
    public string Protect(AuthenticationProperties data, string purpose)
    {
        var key = Guid.NewGuid().ToString();
        var cacheKey = $"{CacheKeyPrefix}-{_name}-{purpose}-{key}";
        var json = ObjectSerializer.ToString(data);

        var options = new DistributedCacheEntryOptions();
        if (data.ExpiresUtc.HasValue)
        {
            options.SetAbsoluteExpiration(data.ExpiresUtc.Value);
        }
        else
        {
            options.SetSlidingExpiration(Constants.DefaultCacheDuration);
        }

        // Rather than encrypt the full AuthenticationProperties
        // cache the data and encrypt the key that points to the data
        Cache.SetString(cacheKey, json, options);

        return Protector.Protect(key);
    }

    /// <summary>
    /// Unprotects the specified protected text.
    /// </summary>
    /// <param name="protectedText">The protected text.</param>
    public AuthenticationProperties Unprotect(string protectedText) => Unprotect(protectedText, null);

    /// <summary>
    /// Unprotects the specified protected text.
    /// </summary>
    /// <param name="protectedText">The protected text.</param>
    /// <param name="purpose">The purpose.</param>
    public AuthenticationProperties Unprotect(string protectedText, string purpose)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return null;
        }

        // Decrypt the key and retrieve the data from the cache.
        var key = Protector.Unprotect(protectedText);
        var cacheKey = $"{CacheKeyPrefix}-{_name}-{purpose}-{key}";
        var json = Cache.GetString(cacheKey);

        if (json == null)
        {
            return null;
        }

        var props = ObjectSerializer.FromString<AuthenticationProperties>(json);
        return props;
    }
}
