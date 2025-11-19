// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Yarp.Internal;

/// <summary>
/// Loads the proxy configuration from the IConfiguration. 
/// </summary>
/// <param name="config"></param>
internal sealed class ProxyBffPluginLoader(

    // This line has been commented out for issue: https://github.com/dotnet/runtime/issues/119883
    //IOptionsMonitor<ProxyConfiguration> proxyConfigMonitor

    // Instead, we read directly from IConfiguration, which is updated when the config file changes.
    [FromKeyedServices(ServiceProviderKeys.ProxyConfigurationKey)] IConfiguration? config = null
    ) : IBffPluginLoader
{
    //private ProxyConfiguration Current => proxyConfigMonitor.CurrentValue;
    private ProxyConfiguration Current => config?.Get<ProxyConfiguration>() ?? new ProxyConfiguration();

    public IBffPlugin? LoadExtension(BffFrontendName name)
    {
        if (!Current.Frontends.TryGetValue(name, out var config))
        {
            return null;
        }

        return new ProxyBffPlugin()
        {
            RemoteApis = config.RemoteApis.Select(MapFrom).ToArray()
        };
    }

    private static RemoteApi MapFrom(RemoteApiConfiguration config)
    {
        Type? type = null;

        if (config.TokenRetrieverTypeName != null)
        {
            type = Type.GetType(config.TokenRetrieverTypeName);
            if (type == null)
            {
                throw new InvalidOperationException($"Type {config.TokenRetrieverTypeName} not found.");
            }
            if (!typeof(IAccessTokenRetriever).IsAssignableFrom(type))
            {
                throw new InvalidOperationException($"Type {config.TokenRetrieverTypeName} must implement IAccessTokenRetriever.");
            }
        }

        var api = new RemoteApi
        {
            PathMatch = config.PathMatch ?? throw new InvalidOperationException($"{nameof(config.PathMatch)} cannot be empty"),
            TargetUri = config.TargetUri ?? throw new InvalidOperationException($"{nameof(config.TargetUri)} cannot be empty"),
            RequiredTokenType = config.RequiredTokenType,
            AccessTokenRetrieverType = type,
            ActivityTimeout = config.ActivityTimeout,
            AllowResponseBuffering = config.AllowResponseBuffering,
            Parameters = Map(config.UserAccessTokenParameters)
        };

        return api;
    }

    private static BffUserAccessTokenParameters? Map(UserAccessTokenParameters? config)
    {
        if (config == null)
        {
            return null;
        }

        return new BffUserAccessTokenParameters
        {
            SignInScheme = config.SignInScheme,
            ChallengeScheme = config.ChallengeScheme,
            ForceRenewal = config.ForceRenewal,
            Resource = config.Resource
        };
    }
}
