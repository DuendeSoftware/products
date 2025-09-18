// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods for BFF endpoint conventions
/// </summary>
public static class EndpointConventionBuilderExtensions
{
    /// <summary>
    /// Marks an endpoint as a local BFF API endpoint.
    /// This metadata is used by the BFF middleware.
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IEndpointConventionBuilder AsBffApiEndpoint(this IEndpointConventionBuilder builder) => builder.WithMetadata(new BffApiAttribute());

    /// <summary>
    /// Adds marker that will cause the BFF framework to skip all antiforgery for this endpoint.
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IEndpointConventionBuilder SkipAntiforgery(this IEndpointConventionBuilder builder) => builder.WithMetadata(new BffApiSkipAntiforgeryAttribute());

    /// <summary>
    /// Adds marker that will cause the BFF framework will not override the HTTP response status code.
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IEndpointConventionBuilder SkipResponseHandling(this IEndpointConventionBuilder builder) => builder.WithMetadata(new BffApiSkipResponseHandlingAttribute());
}
