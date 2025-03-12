// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;

namespace Duende.Bff.Yarp;

/// <summary>
/// Extensions for wiring up YARP middleware
/// </summary>
public static class ProxyAppBuilderExtensions
{
    /// <summary>
    /// Adds antiforgery middleware to YARP pipeline
    /// </summary>
    /// <param name="yarpApp"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseAntiforgeryCheck(this IApplicationBuilder yarpApp)
    {
        return yarpApp.UseMiddleware<AntiforgeryMiddleware>();
    }
}
