// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.MultiSpace.Internal;
using Microsoft.AspNetCore.Builder;

namespace Duende.MultiSpace;

/// <summary>
/// Extension methods for <see cref="IApplicationBuilder"/> to add multi-space resolution middleware.
/// </summary>
public static class MultiSpaceApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the <see cref="SpaceResolutionMiddleware"/> to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseMultiSpaceResolution(this IApplicationBuilder app)
        => app.UseMiddleware<SpaceResolutionMiddleware>();
}
