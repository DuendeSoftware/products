using System;
using Duende.Bff;
using Duende.Bff.Yarp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UserSessionDb.Migrations.UserSessions;

namespace Bff.EF
{
    internal static class Extensions
    {
        public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
        {
            var services = builder.Services;
            var configuration = builder.Configuration;
            services.AddDataProtection()
                .SetApplicationName("JS-EF-Sample");

            // Add BFF services to DI - also add server-side session management
            var cn = configuration.GetConnectionString("db");
            services.AddBff(options =>
            {
                options.BackchannelLogoutAllUserSessions = true;
                options.EnableSessionCleanup = true;    
            })
                .AddRemoteApis()
                .AddEntityFrameworkServerSideSessions(options=> {
                    //options.UseSqlServer(cn);
                    options.UseSqlite(cn, opt => opt.MigrationsAssembly(typeof(UserSessions).Assembly.FullName));
                });

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
                    // host prefixed cookie name
                    options.Cookie.Name = "__Host-spa-ef";
                    options.ExpireTimeSpan  = TimeSpan.FromMinutes(5);
                    // strict SameSite handling
                    options.Cookie.SameSite = SameSiteMode.Strict;
                })
                .AddOpenIdConnect("oidc", options =>
                {
                    options.Authority = "https://localhost:5001";

                    // confidential client using code flow + PKCE
                    options.ClientId = "bff.ef";
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

            // proxy endpoint for cross-site APIs
            // all calls to /api/* will be forwarded to the remote API
            // user or client access token will be attached in API call
            // user access token will be managed automatically using the refresh token
            app.MapRemoteBffApiEndpoint("/api", "https://localhost:5010")
                .RequireAccessToken(TokenType.UserOrClient);

            return app;

        }
    }
}
