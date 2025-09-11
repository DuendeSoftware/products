// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Infrastructure;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Configuration;

internal class ConfigureOpenIdConnectOptions : IPostConfigureOptions<OpenIdConnectOptions>
{
    private readonly string[] _schemes;
    private readonly IServiceProvider _serviceProvider;

    public ConfigureOpenIdConnectOptions(string[] schemes, IServiceProvider serviceProvider)
    {
        _schemes = schemes ?? throw new ArgumentNullException(nameof(schemes));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    private static bool warnedInMemory;

    public void PostConfigure(string name, OpenIdConnectOptions options)
    {
        // no schemes means configure them all
        if (_schemes.Length == 0 || _schemes.Contains(name))
        {
            options.StateDataFormat = new DistributedCacheStateDataFormatter(_serviceProvider, name);
        }

        if (!warnedInMemory)
        {
            var distributedCacheService = _serviceProvider.GetRequiredService<IDistributedCache>();

            if (distributedCacheService is MemoryDistributedCache)
            {
                var logger = _serviceProvider
                    .GetRequiredService<ILogger<ConfigureOpenIdConnectOptions>>();

                logger.LogInformation("You have enabled the OidcStateDataFormatterCache but the distributed cache registered is the default memory based implementation. This will store any OIDC state in memory on the server that initiated the request. If the response is processed on another server it will fail. If you are running in production, you want to switch to a real distributed cache that is shared between all nodes.");

                warnedInMemory = true;
            }
        }
    }
}
