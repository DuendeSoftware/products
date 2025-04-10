// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Hosting.DynamicProviders;

/// <summary>
/// Helper class for configuring authentication options from a dynamic identity provider
/// </summary>
/// <typeparam name="TAuthenticationOptions"></typeparam>
/// <typeparam name="TIdentityProvider"></typeparam>
public abstract class ConfigureAuthenticationOptions<TAuthenticationOptions, TIdentityProvider> : IConfigureNamedOptions<TAuthenticationOptions>
    where TAuthenticationOptions : AuthenticationSchemeOptions
    where TIdentityProvider : IdentityProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ConfigureAuthenticationOptions<TAuthenticationOptions, TIdentityProvider>> _logger;

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="httpContextAccessor"></param>
    /// <param name="logger"></param>
    public ConfigureAuthenticationOptions(IHttpContextAccessor httpContextAccessor, ILogger<ConfigureAuthenticationOptions<TAuthenticationOptions, TIdentityProvider>> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void Configure(string? name, TAuthenticationOptions options)
    {
        if (_httpContextAccessor.HttpContext == null)
        {
            _logger.LogDebug("Failed to configure the dynamic authentication scheme \"{scheme}\" because there is no current HTTP request.", name);
            return;
        }

        // we have to resolve these here due to DI lifetime issues
        var providerOptions = _httpContextAccessor.HttpContext.RequestServices.GetRequiredService<DynamicProviderOptions>();
        var cache = _httpContextAccessor.HttpContext.RequestServices.GetRequiredService<DynamicAuthenticationSchemeCache>();

        var idp = cache.GetIdentityProvider<TIdentityProvider>(name);
        if (idp != null)
        {
            var pathPrefix = providerOptions.PathPrefix + '/' + idp.Scheme;
            var ctx = new ConfigureAuthenticationContext<TAuthenticationOptions, TIdentityProvider>
            {
                IdentityProvider = idp,
                AuthenticationOptions = options,
                DynamicProviderOptions = providerOptions,
                PathPrefix = pathPrefix
            };
            Configure(ctx);
        }
    }

    /// <summary>
    /// Allows for configuring the handler options from the identity provider configuration.
    /// </summary>
    /// <param name="context"></param>
    protected abstract void Configure(ConfigureAuthenticationContext<TAuthenticationOptions, TIdentityProvider> context);

    /// <inheritdoc/>
    public void Configure(TAuthenticationOptions options)
    {
    }
}
