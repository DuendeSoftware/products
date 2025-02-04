// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Api.DPoP;

internal static class Extensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddControllers();

        services.AddAuthentication("token")
            .AddJwtBearer("token", options =>
            {
                options.Authority = "https://localhost:5001";
                options.MapInboundClaims = false;

                options.TokenValidationParameters = new TokenValidationParameters()
                {
                    ValidateAudience = false,
                    ValidTypes = new[] { "at+jwt" },

                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
            });

        // layers DPoP onto the "token" scheme above
        services.ConfigureDPoPTokensForScheme("token");

        services.AddAuthorization(options =>
        {
            options.AddPolicy("ApiCaller", policy =>
            {
                policy.RequireClaim("scope", "api");
            });

            options.AddPolicy("RequireInteractiveUser", policy =>
            {
                policy.RequireClaim("sub");
            });
        });
        return builder.Build();

    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        // The BFF sets the X-Forwarded-* headers to reflect that it
        // forwarded the request here. Using the forwarded headers
        // middleware here would therefore change the request's host to be
        // the bff instead of this API, which is not what the DPoP
        // validation code expects when it checks the htu value. If this API
        // were hosted behind a load balancer, you might need to add back
        // the forwarded headers middleware, or consider changing the DPoP
        // proof validation.

        // app.UseForwardedHeaders(new ForwardedHeadersOptions
        // {
        //     ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
        // });

        app.UseHttpLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers()
            .RequireAuthorization("ApiCaller");

        return app;
    }
}