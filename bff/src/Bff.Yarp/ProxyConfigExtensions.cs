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
    /// <param name="requiredTokenType"></param>
    /// <returns></returns>
    public static RouteConfig WithAccessToken(this RouteConfig config, RequiredTokenType requiredTokenType)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.WithMetadata(Constants.Yarp.TokenTypeMetadata, requiredTokenType.ToString());
    }

    /// <summary>
    /// Adds BFF access token metadata to a route configuration, indicating that
    /// the route should use the user access token if the user is authenticated,
    /// but fall back to an anonymous request if not.
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    [Obsolete("Use TokenRequirement.OptionalUserOrNone")]
    public static RouteConfig WithOptionalUserAccessToken(this RouteConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return WithAccessToken(config, RequiredTokenType.UserOrNone);
    }

    /// <summary>
    /// Adds anti-forgery metadata to a route configuration
    /// </summary>
    /// <param name="config"></param>
    /// <returns></returns>
    public static RouteConfig WithAntiforgeryCheck(this RouteConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.WithMetadata(Constants.Yarp.AntiforgeryCheckMetadata, "true");
    }

    /// <summary>
    /// Adds access token retrieval metadata to a route configuration
    /// </summary>
    /// <param name="config"></param>
    /// <typeparam name="TRetriever"></typeparam>
    /// <returns></returns>
    public static RouteConfig WithAccessTokenRetriever<TRetriever>(this RouteConfig config)
        where TRetriever : IAccessTokenRetriever
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.WithMetadata(Constants.Yarp.AccessTokenRetrieverMetadata, typeof(TRetriever).AssemblyQualifiedName!);
    }

    private static RouteConfig WithMetadata(this RouteConfig config, string key, string value)
    {
        ArgumentNullException.ThrowIfNull(config);

        Dictionary<string, string> metadata;

        if (config.Metadata != null)
        {
            metadata = new Dictionary<string, string>(config.Metadata);
        }
        else
        {
            metadata = new();
        }

        _ = metadata.TryAdd(key, value);

        return config with { Metadata = metadata };
    }


    /// <summary>
    /// Adds BFF access token metadata to a cluster configuration
    /// </summary>
    /// <param name="config"></param>
    /// <param name="requiredTokenType"></param>
    /// <returns></returns>
    public static ClusterConfig WithAccessToken(this ClusterConfig config, RequiredTokenType requiredTokenType)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.WithClusterMetadata(Constants.Yarp.TokenTypeMetadata, requiredTokenType.ToString());
    }

    /// <summary>
    /// Adds access token retrieval metadata to a cluster configuration
    /// </summary>
    /// <param name="config"></param>
    /// <typeparam name="TRetriever">The type implementing <see cref="IAccessTokenRetriever"/> to use for token retrieval.</typeparam>
    /// <returns></returns>
    public static ClusterConfig WithAccessTokenRetriever<TRetriever>(this ClusterConfig config)
        where TRetriever : IAccessTokenRetriever
    {
        ArgumentNullException.ThrowIfNull(config);
        return config.WithClusterMetadata(Constants.Yarp.AccessTokenRetrieverMetadata, typeof(TRetriever).AssemblyQualifiedName!);
    }

    private static ClusterConfig WithClusterMetadata(this ClusterConfig config, string key, string value)
    {
        var metadata = config.Metadata != null
            ? new Dictionary<string, string>(config.Metadata)
            : new Dictionary<string, string>();

        _ = metadata.TryAdd(key, value);

        return config with { Metadata = metadata };
    }
}
