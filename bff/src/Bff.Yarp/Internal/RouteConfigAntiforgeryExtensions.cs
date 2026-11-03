// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Yarp.ReverseProxy.Configuration;

namespace Duende.Bff.Yarp.Internal;

/// <summary>
/// Shared parsing of the "WithAntiforgeryCheck()" route metadata, so that
/// <see cref="AntiForgeryMiddleware"/> (which enforces it) and
/// <see cref="AccessTokenTransformProvider"/> (which warns when it's missing) can never
/// disagree about whether a route is configured to require the anti-forgery header.
/// </summary>
internal static class RouteConfigAntiforgeryExtensions
{
    /// <summary>
    /// True when the route itself (not its cluster - cluster metadata is not read by
    /// <see cref="AntiForgeryMiddleware"/>) is configured via WithAntiforgeryCheck().
    /// </summary>
    internal static bool HasAntiforgeryCheck(this RouteConfig route) =>
        route.Metadata != null
        && route.Metadata.TryGetValue(Constants.Yarp.AntiforgeryCheckMetadata, out var value)
        && string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase);
}
