// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Configuration.DependencyInjection;
using Duende.IdentityServer.Licensing.V2;
using Duende.IdentityServer.Stores;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting.DynamicProviders;

internal class DynamicAuthenticationSchemeProvider : IAuthenticationSchemeProvider
{
    private readonly IAuthenticationSchemeProvider _inner;
    private readonly DynamicProviderOptions _options;
    private readonly LicenseUsageTracker _licenseUsageTracker;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<DynamicAuthenticationSchemeProvider> _logger;

    public DynamicAuthenticationSchemeProvider(
        Decorator<IAuthenticationSchemeProvider> inner,
        DynamicProviderOptions options,
        LicenseUsageTracker licenseUsageTracker,
        IHttpContextAccessor httpContextAccessor,
        ILogger<DynamicAuthenticationSchemeProvider> logger)
    {
        _inner = inner.Instance;
        _options = options;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _licenseUsageTracker = licenseUsageTracker;
    }

    public void AddScheme(AuthenticationScheme scheme)
    {
        _inner.AddScheme(scheme);
    }

    public Task<AuthenticationScheme> GetDefaultAuthenticateSchemeAsync()
    {
        return _inner.GetDefaultAuthenticateSchemeAsync();
    }

    public Task<AuthenticationScheme> GetDefaultChallengeSchemeAsync()
    {
        return _inner.GetDefaultChallengeSchemeAsync();
    }

    public Task<AuthenticationScheme> GetDefaultForbidSchemeAsync()
    {
        return _inner.GetDefaultForbidSchemeAsync();
    }

    public Task<AuthenticationScheme> GetDefaultSignInSchemeAsync()
    {
        return _inner.GetDefaultSignInSchemeAsync();
    }

    public Task<AuthenticationScheme> GetDefaultSignOutSchemeAsync()
    {
        return _inner.GetDefaultSignOutSchemeAsync();
    }

    public Task<IEnumerable<AuthenticationScheme>> GetAllSchemesAsync()
    {
        return _inner.GetAllSchemesAsync();
    }

    public Task<IEnumerable<AuthenticationScheme>> GetRequestHandlerSchemesAsync()
    {
        return _inner.GetRequestHandlerSchemesAsync();
    }

    public async Task<AuthenticationScheme> GetSchemeAsync(string name)
    {
        var scheme = await _inner.GetSchemeAsync(name);

        if (scheme == null)
        {
            return await GetDynamicSchemeAsync(name);
        }

        return scheme;
    }

    public void RemoveScheme(string name)
    {
        _inner.RemoveScheme(name);
    }


    private async Task<AuthenticationScheme> GetDynamicSchemeAsync(string name)
    {
        if (_httpContextAccessor.HttpContext == null)
        {
            _logger.LogDebug("IAuthenticationSchemeProvider being used outside HTTP request, therefore dynamic provider feature can't be used for loading scheme: {scheme}.", name);
            return null;
        }

        // these have to be here because the regular authentication middleware accepts IAuthenticationSchemeProvider
        // as a ctor param, not an Invoke param, which makes it a singleton. Our DynamicAuthenticationSchemeCache
        // and possibly the store is scoped in DI.
        var cache = _httpContextAccessor.HttpContext.RequestServices.GetRequiredService<DynamicAuthenticationSchemeCache>();
        var store = _httpContextAccessor.HttpContext.RequestServices.GetRequiredService<IIdentityProviderStore>();

        var dynamicScheme = cache.Get(name);
        if (dynamicScheme == null)
        {
            var idp = await store.GetBySchemeAsync(name);
            if (idp != null && idp.Enabled)
            {
                var providerType = _options.FindProviderType(idp.Type);
                if (providerType != null)
                {
                    IdentityServerLicenseValidator.Instance.ValidateDynamicProviders();
                    _licenseUsageTracker.FeatureUsed(LicenseFeature.DynamicProviders);
                    dynamicScheme = new DynamicAuthenticationScheme(idp, providerType.HandlerType);
                    cache.Add(name, dynamicScheme);
                }
            }
        }

        return dynamicScheme;
    }
}
