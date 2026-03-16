// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;

namespace Api;

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
        _ = app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
        });

        _ = app.UseHttpLogging();

        if (app.Environment.IsDevelopment())
        {
            _ = app.UseDeveloperExceptionPage();
        }


        _ = app.Map("/static", inner =>
        {
            _ = inner.UseDefaultFiles();
            _ = inner.UseStaticFiles();
        });


        _ = app.UseRouting();
        _ = app.UseAuthentication();
        _ = app.UseAuthorization();

        _ = app.MapControllers()
            .RequireAuthorization("ApiCaller");
        return app;
    }
}
