// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Yarp.Internal;

internal sealed class ProxyBffPluginLoader(IOptionsMonitor<ProxyConfiguration> proxyConfigMonitor) : IBffPluginLoader
{
    private ProxyConfiguration Current => proxyConfigMonitor.CurrentValue;

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
            LocalPath = config.LocalPath ?? throw new InvalidOperationException("localpath cannot be empty"),
            TargetUri = config.TargetUri ?? throw new InvalidOperationException("targeturi cannot be empty"),
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
