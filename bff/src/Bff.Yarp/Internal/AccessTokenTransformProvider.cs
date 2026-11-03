// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Duende.AccessTokenManagement.DPoP;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.Endpoints;
using Duende.Bff.Otel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Duende.Bff.Yarp.Internal;

/// <summary>
/// Transform provider to attach an access token to forwarded calls
/// </summary>
internal class AccessTokenTransformProvider(IOptions<BffOptions> options, ILoggerFactory loggerFactory, IDPoPProofService dPoPProofService) : ITransformProvider
{
    private readonly BffOptions _options = options.Value;

    /// <summary>
    /// Token types that use the current user's identity. A route forwarding one of these
    /// tokens exposes the user's access token to the caller, so it needs CSRF protection.
    /// </summary>
    private static readonly RequiredTokenType[] s_userTokenTypes =
    [
        RequiredTokenType.User,
        RequiredTokenType.UserOrNone,
        RequiredTokenType.UserOrClient
    ];

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
            _ = values.Add(routeValue);
        }

        if (!string.IsNullOrEmpty(clusterValue))
        {
            _ = values.Add(clusterValue);
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
        _ = transformBuildContext.AddRequestTransform(transformContext =>
        {
            if (_options.RemoveCookieHeaderFromYarpRequests)
            {
                _ = transformContext.ProxyRequest.Headers.Remove("Cookie");
            }

            return default;
        });

        if (!GetMetadataValue(transformBuildContext, Constants.Yarp.TokenTypeMetadata, out var tokenTypeMetadata))
        {
            return;
        }

        if (!Enum.TryParse<RequiredTokenType>(tokenTypeMetadata, true, out var tokenType))
        {
            throw new ArgumentException("Invalid value for Duende.Bff.Yarp.TokenType metadata");
        }

        var routeId = transformBuildContext.Route.RouteId;
        var hasAntiforgeryCheckMetadata = transformBuildContext.Route.HasAntiforgeryCheck();

        // Warns at most once per built route transform.
        var hasWarned = new StrongBox<int>();
        var logger = loggerFactory.CreateLogger(typeof(AccessTokenTransformProvider).FullName!);

        _ = transformBuildContext.AddRequestTransform(async transformContext =>
        {
            var httpContext = transformContext.HttpContext;

            httpContext.CheckForBffMiddleware(_options);

            WarnIfMissingAntiForgeryProtection(httpContext, tokenType, hasAntiforgeryCheckMetadata, routeId, logger, hasWarned);

            var accessTokenTransform = new AccessTokenRequestTransform(
                Options.Create(_options),
                dPoPProofService,
                loggerFactory.CreateLogger<AccessTokenRequestTransform>());

            await accessTokenTransform.ApplyAsync(transformContext);
        });
    }

    private void WarnIfMissingAntiForgeryProtection(
        HttpContext httpContext,
        RequiredTokenType tokenType,
        bool hasAntiforgeryCheckMetadata,
        string routeId,
        ILogger logger,
        StrongBox<int> hasWarned)
    {
        if (!s_userTokenTypes.Contains(tokenType))
        {
            // Client tokens aren't an ambient user credential; CSRF is not relevant for them.
            return;
        }

        if (_options.DisableAntiForgeryCheck(httpContext))
        {
            return;
        }

        if (IsRouteProtected(httpContext, hasAntiforgeryCheckMetadata, out var antiforgeryCheckConfiguredButNotEnabled))
        {
            return;
        }

        if (Interlocked.Exchange(ref hasWarned.Value, 1) != 0)
        {
            // Already warned for this route (on this built transform instance).
            return;
        }

        if (antiforgeryCheckConfiguredButNotEnabled)
        {
            logger.YarpRouteAntiForgeryCheckNotEnabled(LogLevel.Warning, routeId);
        }
        else
        {
            logger.YarpRouteMissingAntiForgeryProtection(LogLevel.Warning, routeId);
        }
    }

    internal static bool IsRouteProtected(HttpContext httpContext, bool hasAntiforgeryCheckMetadata, out bool antiforgeryCheckConfiguredButNotEnabled)
    {
        antiforgeryCheckConfiguredButNotEnabled = false;

        var endpoint = httpContext.GetEndpoint();

        if (endpoint?.Metadata.GetMetadata<IBffApiMetadata>() != null)
        {
            if (endpoint.Metadata.GetMetadata<IBffApiSkipAntiForgery>() != null)
            {
                // Explicit opt-out: don't warn.
                return true;
            }

            if (httpContext.BffMiddlewareRanForEndpoint())
            {
                // UseBff() ran for this BFF API endpoint and didn't return 401, so the header
                // check passed.
                return true;
            }
        }

        if (endpoint != null
            && httpContext.Items.TryGetValue(Constants.Middleware.YarpAntiForgeryCheckedEndpoint, out var checkedEndpoint)
            && ReferenceEquals(checkedEndpoint, endpoint))
        {
            // proxyApp.UseAntiforgeryCheck() checked the header for this endpoint.
            return true;
        }

        if (hasAntiforgeryCheckMetadata)
        {
            // WithAntiforgeryCheck() was configured on the route, but no anti-forgery check was
            // recorded for this endpoint - either proxyApp.UseAntiforgeryCheck() was never added
            // to the pipeline, so the check never ran at all.
            antiforgeryCheckConfiguredButNotEnabled = true;
        }

        return false;
    }
}
