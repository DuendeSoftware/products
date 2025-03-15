// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.AccessTokenManagement;
using Duende.Bff.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Duende.Bff.Yarp;

/// <summary>
/// Transform provider to attach an access token to forwarded calls
/// </summary>
public class AccessTokenTransformProvider : ITransformProvider
{
    private readonly BffOptions _options;
    private readonly ILogger<AccessTokenTransformProvider> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IDPoPProofService _dPoPProofService;

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="options"></param>
    /// <param name="logger"></param>
    /// <param name="loggerFactory"></param>
    /// <param name="dPoPProofService"></param>
    public AccessTokenTransformProvider(IOptions<BffOptions> options, ILogger<AccessTokenTransformProvider> logger, ILoggerFactory loggerFactory, IDPoPProofService dPoPProofService)
    {
        _options = options.Value;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _dPoPProofService = dPoPProofService;
    }

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context)
    {
    }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context)
    {
    }

    private static bool GetMetadataValue(TransformBuilderContext transformBuildContext, string metadataName, [NotNullWhen(true)] out string? metadata)
    {
        var routeValue = transformBuildContext.Route.Metadata?.GetValueOrDefault(metadataName);
        var clusterValue =
            transformBuildContext.Cluster?.Metadata?.GetValueOrDefault(metadataName);

        // no metadata
        if (string.IsNullOrEmpty(routeValue) && string.IsNullOrEmpty(clusterValue))
        {
            metadata = null;
            return false;
        }

        var values = new HashSet<string>();
        if (!string.IsNullOrEmpty(routeValue))
        {
            values.Add(routeValue);
        }

        if (!string.IsNullOrEmpty(clusterValue))
        {
            values.Add(clusterValue);
        }

        if (values.Count > 1)
        {
            throw new ArgumentException(
                $"Mismatching {metadataName} route and cluster metadata values found");
        }
        metadata = values.First();
        return true;
    }

    /// <inheritdoc />
    public void Apply(TransformBuilderContext transformBuildContext)
    {
        if (GetMetadataValue(transformBuildContext, Constants.Yarp.OptionalUserTokenMetadata, out var optionalTokenMetadata))
        {
            if (GetMetadataValue(transformBuildContext, Constants.Yarp.TokenTypeMetadata, out var tokenTypeMetadata))
            {
                transformBuildContext.AddRequestTransform(ctx =>
                {
                    ctx.HttpContext.Response.StatusCode = 500;
                    _logger.InvalidRouteConfiguration(transformBuildContext.Route.ClusterId, transformBuildContext.Route.RouteId);

                    return ValueTask.CompletedTask;
                });
                return;
            }
        }
        else if (GetMetadataValue(transformBuildContext, Constants.Yarp.TokenTypeMetadata, out var tokenTypeMetadata))
        {
            if (!Enum.TryParse<TokenType>(tokenTypeMetadata, true, out _))
            {
                throw new ArgumentException("Invalid value for Duende.Bff.Yarp.TokenType metadata");
            }
        }
        else
        {
            return;
        }

        transformBuildContext.AddRequestTransform(async transformContext =>
        {
            transformContext.HttpContext.CheckForBffMiddleware(_options);

            var accessTokenTransform = new AccessTokenRequestTransform(
                Options.Create(_options),
                _dPoPProofService,
                _loggerFactory.CreateLogger<AccessTokenRequestTransform>());

            await accessTokenTransform.ApplyAsync(transformContext);
        });
    }
}
