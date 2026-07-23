// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.MultiSpace.Internal;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="SpaceResolutionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger.</param>
    public SpaceResolutionMiddleware(RequestDelegate next, ILogger<SpaceResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = context.RequestServices.GetRequiredService<IOptions<MultiSpaceOptions>>().Value;
        var spaceStore = context.RequestServices.GetRequiredService<ISpaceStore>();
        var accessor = context.RequestServices.GetRequiredService<ISpaceContextAccessor>();

        var originHeader = context.Request.Headers.Host.FirstOrDefault();
        var originValue = string.IsNullOrEmpty(originHeader)
            ? null
            : $"{context.Request.Scheme}://{originHeader}";

        var pathPrefix = options.SpacePathPrefix?.Value;
        var extractedPath = ExtractSpacePath(context.Request.Path, pathPrefix);

        // For matching, use just the segment part (without prefix)
        // For rewriting, use the full extracted path (prefix + segment)
        var pathForMatching = extractedPath != null && !string.IsNullOrEmpty(pathPrefix)
            ? extractedPath.Substring(pathPrefix.Length) // Remove prefix for matching
            : extractedPath;

        var criteria = new SpaceMatchPattern { Origin = originValue, Path = pathForMatching };
        var result = await spaceStore.TryResolveSpace(criteria, context.RequestAborted);

        // Fallback logic when the combined (origin+path) lookup misses.
        // Hostnames have higher precedence than paths to prevent tenant hopping:
        // - If the origin matches a registered space, that space owns this request — do NOT
        //   fall through to a path-only match (which could route to a different tenant).
        // - Only try path-only resolution when the origin is NOT registered to any space.
        if (result == null && originValue != null && pathForMatching != null)
        {
            var originClaimed = await spaceStore.IsOriginClaimed(originValue, context.RequestAborted);

            if (originClaimed)
            {
                // The origin is registered to a space, but the combined lookup failed.
                // This means the path doesn't belong to this origin's space — reject.
                result = null;
            }
            else
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
                    return;
                }
            }

            LogSpaceResolved(_logger, result.SpaceId, criteria.Origin, criteria.Path);
            accessor.SetSpace(result.SpaceId);
        }
        else if (pathForMatching != null)
        {
            // An explicit path was requested (via the space path prefix) but no space matched.
            // This is always a hard reject — the caller explicitly asked for a specific space
            // that doesn't exist. Falling back to default would be misleading.
            LogSpaceRejected(_logger, criteria.Origin, criteria.Path);
            context.Response.StatusCode = 404;
            return;
        }
        else if (options.FallbackToDefault)
        {
            LogSpaceFallbackToDefault(_logger, criteria.Origin, criteria.Path);
            accessor.SetSpace(SpaceId.Default);
        }
        else
        {
            LogSpaceRejected(_logger, criteria.Origin, criteria.Path);
            context.Response.StatusCode = 404;
            return;
        }

        var spaceId = accessor.IsSpaceIdConfigured() ? accessor.GetSpaceId() : SpaceId.Default;
        using (_logger.BeginScope(new Dictionary<string, object> { ["SpaceId"] = spaceId.Value }))
        {
            await _next(context);
        }
    }

    /// <summary>
    /// Extracts the space path from the request path.
    /// </summary>
    /// <param name="requestPath">The full request path.</param>
    /// <param name="pathPrefix">The optional path prefix (e.g., "/t").</param>
    /// <returns>
    /// If pathPrefix is set and request starts with it: returns prefix + first segment (e.g., "/t/sometenant").
    /// If pathPrefix is not set: returns first segment (e.g., "/sometenant").
    /// Returns null if extraction fails.
    /// Path is normalized to lowercase for case-insensitive matching.
    /// </returns>
    private static string? ExtractSpacePath(PathString requestPath, string? pathPrefix)
    {
        if (string.IsNullOrEmpty(pathPrefix))
        {
            // No prefix configured — extract first segment
            // "/sometenant/abc" → "/sometenant"
            var segments = requestPath.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
#pragma warning disable CA1308 // Normalize strings to uppercase - lowercase is appropriate for URL path matching
            return segments is { Length: > 0 } ? $"/{segments[0].ToLowerInvariant()}" : null;
#pragma warning restore CA1308
        }

        // Prefix configured — check if request starts with it
        if (!requestPath.StartsWithSegments(pathPrefix, StringComparison.OrdinalIgnoreCase, out var remaining))
        {
            return null;
        }

        // Extract first segment after prefix
        // "/t/sometenant/abc" with prefix "/t" → remaining="/sometenant/abc" → extract "/sometenant"
        var remainingSegments = remaining.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (remainingSegments is not { Length: > 0 })
        {
            return null;
        }

        // Normalize to lowercase for case-insensitive matching
#pragma warning disable CA1308 // Normalize strings to uppercase - lowercase is appropriate for URL path matching
        return $"{pathPrefix.ToLowerInvariant()}/{remainingSegments[0].ToLowerInvariant()}";
#pragma warning restore CA1308
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Space resolved to {SpaceId} for origin={Origin}, path={Path}")]
    private static partial void LogSpaceResolved(ILogger logger, SpaceId spaceId, string? origin, string? path);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No space matched for origin={Origin}, path={Path}; falling back to default space")]
    private static partial void LogSpaceFallbackToDefault(ILogger logger, string? origin, string? path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No space matched for origin={Origin}, path={Path}; rejecting request with 404")]
    private static partial void LogSpaceRejected(ILogger logger, string? origin, string? path);
}
