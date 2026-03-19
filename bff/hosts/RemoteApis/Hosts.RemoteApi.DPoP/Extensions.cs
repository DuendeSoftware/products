// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AspNetCore.Authentication.JwtBearer.DPoP;
using Microsoft.IdentityModel.Tokens;

// TODO: remove pragma?
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Api.DPoP;
#pragma warning restore IDE0130

internal static class Extensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        _ = services.AddControllers();

        _ = services.AddAuthentication("token")
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
        _ = services.ConfigureDPoPTokensForScheme("token");

        _ = services.AddAuthorization(options =>
        {
            options.AddPolicy("ApiCaller", policy =>
            {
                _ = policy.RequireClaim("scope", "api");
            });

            options.AddPolicy("RequireInteractiveUser", policy =>
            {
                _ = policy.RequireClaim("sub");
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

        _ = app.UseHttpLogging();

        if (app.Environment.IsDevelopment())
        {
            _ = app.UseDeveloperExceptionPage();
        }

        _ = app.UseRouting();
        _ = app.UseAuthentication();
        _ = app.UseAuthorization();

        _ = app.MapControllers()
            .RequireAuthorization("ApiCaller");

        return app;
    }
}
