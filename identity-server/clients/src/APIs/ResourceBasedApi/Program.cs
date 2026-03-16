// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using ResourceBasedApi;
using Serilog;

SerilogDefaults.Bootstrap();

try
{
    var builder = WebApplication.CreateBuilder(args);

    Console.Title = builder.Environment.ApplicationName;
    Log.Information("{EnvironmentApplicationName} Starting up", builder.Environment.ApplicationName);

    builder.ConfigureSerilogDefaults();
    _ = builder.AddServiceDefaults();

    _ = builder.Services.AddControllers();
    _ = builder.Services.AddCors();

    _ = builder.Services.AddAuthentication("token")
        // JWT tokens
        .AddJwtBearer("token", options =>
        {
            options.Authority = builder.Configuration["is-host"];
            options.Audience = "urn:resource1";
            options.MapInboundClaims = false;
            options.TokenValidationParameters.ValidTypes = ["at+jwt"];

            // if token does not contain a dot, it is a reference token
            options.ForwardDefaultSelector = Selector.ForwardReferenceToken("introspection");
        })

        // reference tokens
        .AddOAuth2Introspection("introspection", options =>
        {
            options.Authority = builder.Configuration["is-host"];
            options.ClientId = "urn:resource1";
            options.ClientSecret = "secret";
        });

    var app = builder.Build();

    _ = app.UseSerilogRequestLogging();

    _ = app.UseCors(policy =>
    {
        _ = policy.WithOrigins("https://localhost:44300");
        _ = policy.AllowAnyHeader();
        _ = policy.AllowAnyMethod();
        _ = policy.WithExposedHeaders("WWW-Authenticate");
    });

    _ = app.UseRouting();
    _ = app.UseAuthentication();
    _ = app.UseAuthorization();

    _ = app.MapControllers().RequireAuthorization();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.CloseAndFlush();
}
