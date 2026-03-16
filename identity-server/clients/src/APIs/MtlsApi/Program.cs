// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using MtlsApi;
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

    // this API will accept any access token from the authority
    _ = builder.Services.AddAuthentication("token")
        .AddJwtBearer("token", options =>
        {
            options.Authority = builder.Configuration["is-host"];
            options.TokenValidationParameters.ValidateAudience = false;
            options.MapInboundClaims = false;
            options.TokenValidationParameters.ValidTypes = ["at+jwt"];
        });

    _ = builder.Services.Configure<KestrelServerOptions>(options =>
    {
        options.ConfigureHttpsDefaults(https =>
        {
            https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
            https.AllowAnyClientCertificate(); // Needed for the "ephemeral" mtls client
        });
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
    _ = app.UseConfirmationValidation();
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
