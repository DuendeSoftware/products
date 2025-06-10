// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Bff.Benchmarks.Hosts;

public class ApiHost : Host
{
    public ApiHost(Uri identityServerUri) : base()
    {
        OnConfigureServices += services =>
        {
            services.AddAuthentication("token")
                .AddJwtBearer("token", options =>
                {
                    options.Authority = identityServerUri.ToString();
                    options.MapInboundClaims = false;
                });
        };

        OnConfigure += app =>
        {
            app.Use(async (c, n) =>
            {
                await n();
            });

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapGet("{**catch-all}", () => "ok");

        };

    }
}
