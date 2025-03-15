// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Clients;
using DPoPApi;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog early
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("SampleApi", LogEventLevel.Debug)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}")
    .CreateLogger();

builder.Host.UseSerilog();

// Include the service defaults from Aspire
builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddCors();

// this API will accept any access token from the authority
builder.Services.AddAuthentication("token")
    .AddJwtBearer("token", options =>
    {
        options.Authority = Constants.Authority;
        options.TokenValidationParameters.ValidateAudience = false;
        options.MapInboundClaims = false;

        options.TokenValidationParameters.ValidTypes = new[] { "at+jwt" };
    });

builder.Services.ConfigureDPoPTokensForScheme("token", options =>
{
    options.Mode = DPoPMode.DPoPAndBearer;
});

var app = builder.Build();

app.UseCors(policy =>
{
    policy.WithOrigins("https://localhost:44300");
    policy.AllowAnyHeader();
    policy.AllowAnyMethod();
    policy.WithExposedHeaders("WWW-Authenticate");
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireAuthorization();

app.Run();
