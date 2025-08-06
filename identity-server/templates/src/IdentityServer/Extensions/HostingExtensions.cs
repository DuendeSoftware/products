using System.IdentityModel.Tokens.Jwt;
using Duende.IdentityServer;
using IdentityServerTemplate.Pages.Admin.ApiScopes;
using IdentityServerTemplate.Pages.Admin.Clients;
using IdentityServerTemplate.Pages.Admin.IdentityScopes;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace IdentityServerTemplate.Extensions;

internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();

        builder.Services.AddRazorPages()
            .AddRazorRuntimeCompilation();

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

        // Configure the JwtSecurityTokenHandler to not map inbound claims automatically
        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        var isBuilder = builder.Services
            .AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;

                options.ServerSideSessions.UserDisplayNameClaimType = "name";
            })
            .AddTestUsers(TestUsers.Users)
            // this adds the config data from DB (clients, resources, CORS)
            .AddConfigurationStore(options =>
            {
                options.ConfigureDbContext = b =>
                    b.UseSqlite(connectionString,
                        dbOpts => dbOpts.MigrationsAssembly(typeof(Program).Assembly.FullName));
            })
            // this is something you will want in production to reduce load on and requests to the DB
            //.AddConfigurationStoreCache()
            //
            // this adds the operational data from DB (codes, tokens, consents)
            .AddOperationalStore(options =>
            {
                options.ConfigureDbContext = b =>
                    b.UseSqlite(connectionString,
                        dbOpts => dbOpts.MigrationsAssembly(typeof(Program).Assembly.FullName));
            })
            .AddServerSideSessions()
            .AddLicenseSummary();

        // Adds configuration to use Duende's Demo IdentityServer instance.
        builder.Services.AddAuthentication()
            .AddOpenIdConnect("oidc", "Duende Demo", options =>
            {
                options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                options.SignOutScheme = IdentityServerConstants.SignoutScheme;
                options.SaveTokens = true;

                options.Authority = "https://demo.duendesoftware.com";
                options.ClientId = "interactive.confidential";
                options.ClientSecret = "secret";
                options.ResponseType = "code";

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };

                options.GetClaimsFromUserInfoEndpoint = true;

                options.Scope.Add("openid");
                options.Scope.Add("profile");

            });

        // this adds the necessary config for the simple admin/config pages
        {
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(Config.Policies.Admin,
                    policy => policy.RequireClaim("role", "admin"));
            });

            builder.Services.AddTransient<ClientRepository>();
            builder.Services.AddTransient<IdentityScopeRepository>();
            builder.Services.AddTransient<ApiScopeRepository>();
        }

        // this adds the necessary config for the portal page
        builder.Services.AddTransient<Pages.Portal.ClientRepository>();

        return builder.Build();
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // Content Security Policy options
        app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("Content-Security-Policy",
                "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-src 'none';");
            await next();
        });

        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
            },
            ServeUnknownFileTypes = false,
            ContentTypeProvider = new FileExtensionContentTypeProvider
            {
                Mappings =
                {
                    [".woff2"] = "font/woff2",
                    [".woff"] = "font/woff"
                }
            }
        });

        app.UseRouting();
        app.UseIdentityServer();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapRazorPages()
            .RequireAuthorization();

        return app;
    }
}
