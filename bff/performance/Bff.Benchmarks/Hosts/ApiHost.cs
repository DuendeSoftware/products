// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Bff.Benchmarks.Hosts;

public class ApiHost : Host
{
    internal ApiHost(Uri identityServerUri, SimulatedInternet simulatedInternet) : base(new Uri("https://api"), simulatedInternet)
    {
        OnConfigureServices += services =>
        {
            _ = services.AddAuthentication("token")
                .AddJwtBearer("token", options =>
                {
                    options.Authority = identityServerUri.ToString();
                    options.MapInboundClaims = false;
                    //options.BackchannelHttpHandler = simulatedInternet;
                });
        };

        OnConfigure += app =>
        {
            _ = app.Use(async (c, n) =>
            {
                await n();
            });

            _ = app.UseRouting();

            _ = app.UseAuthentication();
            _ = app.UseAuthorization();

            _ = app.MapGet("{**catch-all}", () => "ok");

        };

    }
}
