// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal;
using Microsoft.AspNetCore.Builder;

namespace Duende.Spaces;

/// <summary>
/// Extension methods for <see cref="IApplicationBuilder"/> to add Spaces resolution middleware.
/// </summary>
public static class SpacesApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the <see cref="SpaceResolutionMiddleware"/> to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder.</returns>
    public static IApplicationBuilder UseSpaceResolution(this IApplicationBuilder app)
        => app.UseMiddleware<SpaceResolutionMiddleware>();
}
