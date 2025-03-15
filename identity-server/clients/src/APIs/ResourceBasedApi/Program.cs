// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using ResourceBasedApi;
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
builder.Services.AddDistributedMemoryCache();

builder.Services.AddAuthentication("token")

    // JWT tokens
    .AddJwtBearer("token", options =>
    {
        options.Authority = "https://localhost:5001";
        options.Audience = "urn:resource1";

        options.TokenValidationParameters.ValidTypes = new[] { "at+jwt" };
        options.MapInboundClaims = false;

        // if token does not contain a dot, it is a reference token
        options.ForwardDefaultSelector = Selector.ForwardReferenceToken("introspection");
    })

    // reference tokens
    .AddOAuth2Introspection("introspection", options =>
    {
        options.Authority = "https://localhost:5001";

        options.ClientId = "urn:resource1";
        options.ClientSecret = "secret";
    });

var app = builder.Build();

app.UseCors(policy =>
{
    policy.WithOrigins(
        "https://localhost:44300");

    policy.AllowAnyHeader();
    policy.AllowAnyMethod();
    policy.WithExposedHeaders("WWW-Authenticate");
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers().RequireAuthorization();

app.Run();
