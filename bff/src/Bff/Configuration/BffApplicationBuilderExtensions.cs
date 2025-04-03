// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Endpoints;

namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Extension methods for the BFF middleware
/// </summary>
public static class BffApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Duende.BFF middleware to the pipeline
    /// </summary>
    /// <param name="app"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseBff(this IApplicationBuilder app)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return app.UseMiddleware<BffMiddleware>();
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
