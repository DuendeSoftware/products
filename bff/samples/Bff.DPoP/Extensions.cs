// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using Duende.Bff;
using Duende.Bff.Yarp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Yarp.ReverseProxy.Configuration;

namespace Bff.DPoP;

internal static class Extensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var yarpBuilder = services.AddReverseProxy()
               .AddBffExtensions();

        yarpBuilder.LoadFromMemory(
            new[]
            {
                    new RouteConfig()
                    {
                        RouteId = "user-token",
                        ClusterId = "cluster1",

                        Match = new()
                        {
                            Path = "/yarp/user-token/{**catch-all}"
                        }
                    }.WithAccessToken(TokenType.User).WithAntiforgeryCheck(),
                    new RouteConfig()
                    {
                        RouteId = "client-token",
                        ClusterId = "cluster1",

                        Match = new()
                        {
                            Path = "/yarp/client-token/{**catch-all}"
                        }
                    }.WithAccessToken(TokenType.Client).WithAntiforgeryCheck(),
                    new RouteConfig()
                    {
                        RouteId = "user-or-client-token",
                        ClusterId = "cluster1",

                        Match = new()
                        {
                            Path = "/yarp/user-or-client-token/{**catch-all}"
                        }
                    }.WithAccessToken(TokenType.UserOrClient).WithAntiforgeryCheck(),
                    new RouteConfig()
                    {
                        RouteId = "anonymous",
                        ClusterId = "cluster1",

                        Match = new()
                        {
                            Path = "/yarp/anonymous/{**catch-all}"
                        }
                    }.WithAntiforgeryCheck()
            },
            new[]
            {
                    new ClusterConfig
                    {
                        ClusterId = "cluster1",

                        Destinations = new Dictionary<string, DestinationConfig>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "destination1", new() { Address = "https://localhost:5011" } },
                        }
                    }
            });

        // Add BFF services to DI - also add server-side session management
        services.AddBff(options =>
        {
            var rsaKey = new RsaSecurityKey(RSA.Create(2048));
            var jwkKey = JsonWebKeyConverter.ConvertFromSecurityKey(rsaKey);
            jwkKey.Alg = "PS256";
            var jwk = JsonSerializer.Serialize(jwkKey);
            options.DPoPJsonWebKey = jwk;
        })
        .AddRemoteApis()
        .AddServerSideSessions();

        // local APIs
        services.AddControllers();

        // cookie options
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = "cookie";
                options.DefaultChallengeScheme = "oidc";
                options.DefaultSignOutScheme = "oidc";
            })
            .AddCookie("cookie", options =>
            {
                // set session lifetime
                options.ExpireTimeSpan = TimeSpan.FromHours(8);

                // sliding or absolute
                options.SlidingExpiration = false;

                // host prefixed cookie name
                options.Cookie.Name = "__Host-spa6-dpop";

                // strict SameSite handling
                options.Cookie.SameSite = SameSiteMode.Strict;
            })
            .AddOpenIdConnect("oidc", options =>
            {
                options.Authority = "https://localhost:5001";

                // confidential client using code flow + PKCE
                options.ClientId = "bff.dpop";
                options.ClientSecret = "secret";
                options.ResponseType = "code";
                options.ResponseMode = "query";

                options.MapInboundClaims = false;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;

                // request scopes + refresh tokens
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("api");
                options.Scope.Add("offline_access");
            });

        services.AddUserAccessTokenHttpClient("api",
            configureClient: client =>
            {
                client.BaseAddress = new Uri("https://localhost:5011/api");
            });

        return builder.Build();
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseHttpLogging();
        app.UseDeveloperExceptionPage();

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseRouting();

        // adds antiforgery protection for local APIs
        app.UseBff();

        // adds authorization for local and remote API endpoints
        app.UseAuthorization();

        // local APIs
        app.MapControllers()
            .RequireAuthorization()
            .AsBffApiEndpoint();

        // login, logout, user, backchannel logout...
        app.MapBffManagementEndpoints();

        // proxy endpoints using yarp
        app.MapReverseProxy(proxyApp =>
        {
            proxyApp.UseAntiforgeryCheck();
        });

        // proxy endpoints using BFF's simplified wrapper
        MapRemoteUrls(app);
        return app;
    }

    private static void MapRemoteUrls(IEndpointRouteBuilder app)
    {
        // On this path, we use a client credentials token
        app.MapRemoteBffApiEndpoint("/api/client-token", "https://localhost:5011")
            .RequireAccessToken(TokenType.Client);

        // On this path, we use a user token if logged in, and fall back to a client credentials token if not
        app.MapRemoteBffApiEndpoint("/api/user-or-client-token", "https://localhost:5011")
            .RequireAccessToken(TokenType.UserOrClient);

        // On this path, we make anonymous requests
        app.MapRemoteBffApiEndpoint("/api/anonymous", "https://localhost:5011");

        // On this path, we use the client token only if the user is logged in
        app.MapRemoteBffApiEndpoint("/api/optional-user-token", "https://localhost:5011")
            .WithOptionalUserAccessToken();

        // On this path, we require the user token
        app.MapRemoteBffApiEndpoint("/api/user-token", "https://localhost:5011")
            .RequireAccessToken();
    }
}
