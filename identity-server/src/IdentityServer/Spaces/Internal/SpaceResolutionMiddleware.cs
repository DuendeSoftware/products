// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Spaces.Internal.Licensing;
using Duende.Spaces.Internal.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.Spaces.Internal;

/// <summary>
/// Middleware that resolves the space context for each request, optionally rewriting
/// the request path when a path-based space match is found.
/// </summary>
/// <remarks>
/// Uses <see cref="ISpaceStore"/> to determine the appropriate space, delegates path
/// rewriting to <see cref="ISpacePathRewriter"/>, and sets the resolved space via
/// <see cref="ISpaceContextAccessor.SetSpace(SpaceId)"/>.
/// </remarks>
internal sealed partial class SpaceResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SpaceResolutionMiddleware> _logger;
    private readonly SpacesLicenseValidator _licenseValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpaceResolutionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="licenseValidator">The Spaces license validator.</param>
    public SpaceResolutionMiddleware(
        RequestDelegate next,
        ILogger<SpaceResolutionMiddleware> logger,
        SpacesLicenseValidator licenseValidator)
    {
        _next = next;
        _logger = logger;
        _licenseValidator = licenseValidator;
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = context.RequestServices.GetRequiredService<IOptions<SpacesOptions>>().Value;
        var spaceStore = context.RequestServices.GetRequiredService<ISpaceStore>();
        var accessor = context.RequestServices.GetRequiredService<ISpaceContextAccessor>();

        if (accessor.IsSpaceIdConfigured())
        {
            LogSpaceIdIsAlreadyConfiguredSoSkippingResolution(_logger, LogLevel.Debug);
            await InvokeNextWithSpaceScopedCallbacksAsync(context, accessor);
            return;
        }

        var originHeader = context.Request.Headers.Host.FirstOrDefault();
        var originValue = string.IsNullOrEmpty(originHeader)
            ? null
            : $"{context.Request.Scheme}://{originHeader}";

        var pathPrefix = options.SpacePathPrefix?.Value;
        var extractedPath = ExtractSpacePath(context.Request.Path, pathPrefix);
#pragma warning disable CA1308 // Space paths are only resolved in lowercase
        var lowercasePath = extractedPath?.ToLowerInvariant();
#pragma warning restore CA1308

        if (!string.Equals(extractedPath, lowercasePath, StringComparison.Ordinal))
        {
            LogSpacePathNotLowercase(_logger, LogLevel.Warning, extractedPath!, lowercasePath!);
            context.Response.StatusCode = 404;
            return;
        }

        // For matching, use just the segment part (without prefix)
        // For rewriting, use the full extracted path (prefix + segment)
        var pathForMatching = extractedPath != null && !string.IsNullOrEmpty(pathPrefix)
            ? extractedPath.Substring(pathPrefix.Length) // Remove prefix for matching
            : extractedPath;

        var criteria = new SpaceMatchPattern { Origin = originValue, Path = pathForMatching };
        var resolvedSpaceId = await ResolveSpaceId(context, spaceStore, options, criteria, originValue, pathForMatching, extractedPath);

        if (resolvedSpaceId is null)
        {
            // All rejection paths already set the status code and returned.
            return;
        }

        await _licenseValidator.ValidateSpaceCountAsync(
            ct => context.RequestServices.GetRequiredService<SpaceRepository>().GetActiveCountAsync(ct),
            Ct.None);

        using (accessor.SetSpace(resolvedSpaceId))
        {
            await InvokeNextWithSpaceScopedCallbacksAsync(context, accessor);
        }
    }

    private async Task InvokeNextWithSpaceScopedCallbacksAsync(HttpContext context, ISpaceContextAccessor accessor)
    {
        var responseFeature = context.Features.GetRequiredFeature<IHttpResponseFeature>();
        context.Features.Set<IHttpResponseFeature>(new SpaceScopedResponseFeature(responseFeature, accessor));
        try
        {
            await _next(context);
        }
        finally
        {
            context.Features.Set(responseFeature);
        }
    }

    private async Task<SpaceId?> ResolveSpaceId(
        HttpContext context,
        ISpaceStore spaceStore,
        SpacesOptions options,
        SpaceMatchPattern criteria,
        string? originValue,
        string? pathForMatching,
        string? extractedPath)
    {
        var result = await spaceStore.TryResolveSpace(criteria, context.RequestAborted);

        // Fallback logic when the combined (origin+path) lookup misses.
        // Hostnames have higher precedence than paths to prevent tenant hopping:
        // - If the origin matches a registered space, that space owns this request — do NOT
        //   fall through to a path-only match (which could route to a different tenant).
        // - Only try path-only resolution when the origin is NOT registered to any space.
        if (result == null && originValue != null && pathForMatching != null)
        {
            var originClaimed = await spaceStore.IsOriginClaimed(originValue, context.RequestAborted);

            if (!originClaimed)
            {
                // Origin is not registered — allow path-only resolution for path-based tenants.
                result = await spaceStore.TryResolveSpace(
                    new SpaceMatchPattern { Path = pathForMatching }, context.RequestAborted);
            }
        }

        if (result != null)
        {
            if (result.MatchedPath != null)
            {
                var rewriter = context.RequestServices.GetRequiredService<ISpacePathRewriter>();
                // Rewrite using the full extracted path (prefix + segment)
                var pathToRewrite = extractedPath ?? result.MatchedPath.Value;
                if (!rewriter.TryRewrite(context, pathToRewrite))
                {
                    context.Response.StatusCode = 404;
                    return null;
                }
            }

            LogSpaceResolved(_logger, LogLevel.Debug, result.SpaceId, criteria.Origin, criteria.Path);
            return result.SpaceId;
        }

        if (pathForMatching != null)
        {
            // An explicit path was requested (via the space path prefix) but no space matched.
            // This is always a hard reject — the caller explicitly asked for a specific space
            // that doesn't exist. Falling back to default would be misleading.
            LogSpaceRejected(_logger, LogLevel.Warning, criteria.Origin, criteria.Path);
            context.Response.StatusCode = 404;
            return null;
        }

        if (options.FallbackToDefault)
        {
            LogSpaceFallbackToDefault(_logger, LogLevel.Debug, criteria.Origin, criteria.Path);
            return SpaceId.Default;
        }

        LogSpaceRejected(_logger, LogLevel.Debug, criteria.Origin, criteria.Path);
        context.Response.StatusCode = 404;
        return null;
    }

    /// <summary>
    /// Extracts the space path from the request path, exactly as requested.
    /// </summary>
    /// <param name="requestPath">The full request path.</param>
    /// <param name="pathPrefix">The optional path prefix (e.g., "/t").</param>
    /// <returns>
    /// If pathPrefix is set and request starts with it (ignoring case): returns the requested prefix + first segment (e.g., "/T/Sometenant").
    /// If pathPrefix is not set: returns first segment (e.g., "/Sometenant").
    /// Returns null if extraction fails.
    /// </returns>
    private static string? ExtractSpacePath(PathString requestPath, string? pathPrefix)
    {
        if (string.IsNullOrEmpty(pathPrefix))
        {
            // No prefix configured — extract first segment
            // "/sometenant/abc" ? "/sometenant"
            var segments = requestPath.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
            return segments is { Length: > 0 } ? $"/{segments[0]}" : null;
        }

        // Matched ignoring case so that a mixed-case prefix is rejected with a warning instead of being
        // treated as a request with no space path.
        if (!requestPath.StartsWithSegments(pathPrefix, StringComparison.OrdinalIgnoreCase, out var remaining))
        {
            return null;
        }

        // Extract first segment after prefix
        // "/t/sometenant/abc" with prefix "/t" ? remaining="/sometenant/abc" ? extract "/sometenant"
        var remainingSegments = remaining.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (remainingSegments is not { Length: > 0 })
        {
            return null;
        }

        return $"{requestPath.Value![..pathPrefix.Length]}/{remainingSegments[0]}";
    }

    [LoggerMessage(Message = "Space path {RequestedPath} is not lowercase; rejecting request with 404. Space paths are only resolved in lowercase, so {LowercasePath} should have been used")]
    private static partial void LogSpacePathNotLowercase(ILogger logger, LogLevel logLevel, string requestedPath, string lowercasePath);

    [LoggerMessage(Message = "Space resolved to {SpaceId} for origin={Origin}, path={Path}")]
    private static partial void LogSpaceResolved(ILogger logger, LogLevel logLevel, SpaceId spaceId, string? origin, string? path);

    [LoggerMessage(Message = "No space matched for origin={Origin}, path={Path}; falling back to default space")]
    private static partial void LogSpaceFallbackToDefault(ILogger logger, LogLevel logLevel, string? origin, string? path);

    [LoggerMessage(Message = "No space matched for origin={Origin}, path={Path}; rejecting request with 404")]
    private static partial void LogSpaceRejected(ILogger logger, LogLevel logLevel, string? origin, string? path);

    [LoggerMessage(Message = "SpaceId is already configured so skipping resolution.")]
    private static partial void LogSpaceIdIsAlreadyConfiguredSoSkippingResolution(ILogger logger, LogLevel logLevel);
}
