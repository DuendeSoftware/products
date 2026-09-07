// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.UI.Infra;
using Microsoft.AspNetCore.Builder;

namespace Duende.IdentityServer.Interaction.SharedHosts.Api;

public sealed class ApiHost : TestHost
{
    private readonly string _authority;
    private readonly string? _audience;
    private readonly Action<IServiceCollection>? _configureServices;
    private readonly string? _introspectionClientId;
    private readonly string? _introspectionClientSecret;

    public ApiHost(
        IScenarioConfigurator configurator,
        string name,
        string authority) : this(configurator, name, authority, null, null, null, null)
    {
    }

    public ApiHost(
        IScenarioConfigurator configurator,
        string name,
        string authority,
        Action<IServiceCollection> configureServices) :
        this(configurator, name, authority, configureServices, null, null, null)
    {
    }

    public ApiHost(
        IScenarioConfigurator configurator,
        string name,
        string authority,
        string audience,
        string introspectionClientId,
        string introspectionClientSecret) :
        this(configurator, name, authority, null, audience, introspectionClientId, introspectionClientSecret)
    {
    }

    private ApiHost(
        IScenarioConfigurator configurator,
        string name,
        string authority,
        Action<IServiceCollection>? configureServices,
        string? audience,
        string? introspectionClientId,
        string? introspectionClientSecret) : base(configurator, name)
    {
        _authority = authority;
        _configureServices = configureServices;
        _audience = audience;
        _introspectionClientId = introspectionClientId;
        _introspectionClientSecret = introspectionClientSecret;
    }

    protected override WebApplication CreateApp(WebApplicationBuilder builder)
    {
        _configureServices?.Invoke(builder.Services);

        builder.Services.AddControllers();

        builder.Services.AddAuthentication("token")
            .AddJwtBearer("token", options =>
            {
                options.Authority = _authority;
                if (_audience == null)
                {
                    options.TokenValidationParameters.ValidateAudience = false;
                }
                else
                {
                    options.Audience = _audience;
                    options.ForwardDefaultSelector = ForwardReferenceToken;
                }
                options.MapInboundClaims = false;
                options.TokenValidationParameters.ValidTypes = ["at+jwt"];
            });

        if (_introspectionClientId != null && _introspectionClientSecret != null)
        {
            builder.Services.AddAuthentication()
                .AddOAuth2Introspection("introspection", options =>
                {
                    options.Authority = _authority;
                    options.ClientId = _introspectionClientId;
                    options.ClientSecret = _introspectionClientSecret;
                });
        }

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

    private static string? ForwardReferenceToken(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        const string bearerPrefix = "Bearer ";

        return authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase) &&
            !authorization.AsSpan(bearerPrefix.Length).Contains('.')
                ? "introspection"
                : null;
    }
}
