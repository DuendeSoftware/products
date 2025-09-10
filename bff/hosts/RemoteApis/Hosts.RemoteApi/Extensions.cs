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
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
        });

        app.UseHttpLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        
        app.Map("/static", inner =>
        {
            inner.UseDefaultFiles();
            inner.UseStaticFiles();
        });
        

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers()
            .RequireAuthorization("ApiCaller");
        return app;
    }
}

