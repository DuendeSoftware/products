// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable 1591

namespace Duende.IdentityServer.Hosting;

public static class CorsMiddlewareExtensions
{
    public static void ConfigureCors(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetRequiredService<IdentityServerOptions>();
        _ = app.UseCors(options.Cors.CorsPolicyName);
    }
}
