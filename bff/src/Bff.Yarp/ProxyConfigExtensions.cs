// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Yarp.ReverseProxy.Configuration;

namespace Duende.Bff.Yarp;

/// <summary>
/// Extension methods for YARP configuration
/// </summary>
public static class ProxyConfigExtensions
{
    /// <summary>
    /// Adds BFF access token metadata to a route configuration
    /// </summary>
    /// <param name="config"></param>
    /// <param name="tokenType"></param>
    /// <returns></returns>
    public static RouteConfig WithAccessToken(this RouteConfig config, TokenType tokenType) => config.WithMetadata(Constants.Yarp.TokenTypeMetadata, tokenType.ToString());

    /// <summary>
    /// Adds BFF access token metadata to a route configuration, indicating that 
    /// the route should use the user access token if the user is authenticated,
    /// but fall back to an anonymous request if not.
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    public static RouteConfig WithOptionalUserAccessToken(this RouteConfig config) => config.WithMetadata(Constants.Yarp.OptionalUserTokenMetadata, "true");

    /// <summary>
    /// Adds anti-forgery metadata to a route configuration
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    public static RouteConfig WithAntiforgeryCheck(this RouteConfig config) => config.WithMetadata(Constants.Yarp.AntiforgeryCheckMetadata, "true");

    private static RouteConfig WithMetadata(this RouteConfig config, string key, string value)
    {
        Dictionary<string, string> metadata;

        if (config.Metadata != null)
        {
            metadata = new Dictionary<string, string>(config.Metadata);
        }
        else
        {
            metadata = new();
        }

        metadata.TryAdd(key, value);

        return config with { Metadata = metadata };
    }


    /// <summary>
    /// Adds BFF access token metadata to a cluster configuration
    /// </summary>
    /// <param name="config"></param>
    /// <param name="tokenType"></param>
    /// <returns></returns>
    public static ClusterConfig WithAccessToken(this ClusterConfig config, TokenType tokenType)
    {
        Dictionary<string, string> metadata;

        if (config.Metadata != null)
        {
            metadata = new Dictionary<string, string>(config.Metadata);
        }
        else
        {
            metadata = new();
        }

        metadata.TryAdd(Constants.Yarp.TokenTypeMetadata, tokenType.ToString());

        return config with { Metadata = metadata };
    }
}
