// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.UI.Infra;
using Microsoft.AspNetCore.Builder;

namespace Duende.IdentityServer.Interaction.SharedHosts.Api;

public class ApiHost(
    IScenarioConfigurator configurator,
    string name,
    string authority,
    Action<IServiceCollection>? configureServices = null) : TestHost(configurator, name)
{
    protected override WebApplication CreateApp(WebApplicationBuilder builder)
    {
        configureServices?.Invoke(builder.Services);

        builder.Services.AddControllers();

        builder.Services.AddAuthentication("token")
            .AddJwtBearer("token", options =>
            {
                options.Authority = authority;
                options.TokenValidationParameters.ValidateAudience = false;
                options.MapInboundClaims = false;
                options.TokenValidationParameters.ValidTypes = ["at+jwt"];
            });

        builder.Services.AddAuthorization();

        var app = builder.Build();

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/identity", (HttpContext c) =>
        {
            return c.User.Claims.Select(c => new { c.Type, c.Value });
        }).RequireAuthorization();

        return app;
    }
}
