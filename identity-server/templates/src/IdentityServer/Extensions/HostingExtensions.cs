using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using Duende.IdentityServer;
using IdentityServerTemplate.Pages.Admin.ApiScopes;
using IdentityServerTemplate.Pages.Admin.Clients;
using IdentityServerTemplate.Pages.Admin.IdentityScopes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Filters;

namespace IdentityServerTemplate.Extensions;

internal static class HostingExtensions
{

    public static WebApplicationBuilder ConfigureLogging(this WebApplicationBuilder builder)
    {
        // Write most logs to the console but diagnostic data to a file.
        // See https://docs.duendesoftware.com/identityserver/diagnostics/data
        _ = builder.Services.AddSerilog(lc =>
        {
            _ = lc.WriteTo.Logger(consoleLogger =>
            {
                _ = consoleLogger.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
                    formatProvider: CultureInfo.InvariantCulture);
                if (builder.Environment.IsDevelopment())
                {
                    _ = consoleLogger.Filter.ByExcluding(Matching.FromSource("Duende.IdentityServer.Diagnostics.Summary"));
                }
            });
            if (builder.Environment.IsDevelopment())
            {
                _ = lc.WriteTo.Logger(fileLogger =>
                {
                    _ = fileLogger
                        .WriteTo.File("./diagnostics/diagnostic.log", rollingInterval: RollingInterval.Day,
                            fileSizeLimitBytes: 1024 * 1024 * 10, // 10 MB
                            rollOnFileSizeLimit: true,
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
                            formatProvider: CultureInfo.InvariantCulture)
                        .Filter
                        .ByIncludingOnly(Matching.FromSource("Duende.IdentityServer.Diagnostics.Summary"));
                }).Enrich.FromLogContext().ReadFrom.Configuration(builder.Configuration);
            }
        });
        return builder;
    }

    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        _ = builder.Services.AddHttpContextAccessor();

        _ = builder.Services.AddRazorPages()
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

                // Use a large chunk size for diagnostic logs in development where it will be redirected to a local file
                if (builder.Environment.IsDevelopment())
                {
                    options.Diagnostics.ChunkSize = 1024 * 1024 * 10; // 10 MB
                }
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
        _ = builder.Services.AddAuthentication()
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
            _ = builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(Config.Policies.Admin,
                    policy => policy.RequireClaim("role", "admin"));
            });

            _ = builder.Services.AddTransient<ClientRepository>();
            _ = builder.Services.AddTransient<IdentityScopeRepository>();
            _ = builder.Services.AddTransient<ApiScopeRepository>();
        }

        // Add `.PersistKeysTo…()` and `.ProtectKeysWith…()` calls
        // See more at https://docs.duendesoftware.com/general/data-protection
        _ = builder.Services.AddDataProtection()
            .SetApplicationName("IdentityServer");

        // this adds the necessary config for the portal page
        _ = builder.Services.AddTransient<Pages.Portal.ClientRepository>();

        return builder.Build();
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        _ = app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            _ = app.UseDeveloperExceptionPage();
        }

        // Content Security Policy options
        _ = app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("Content-Security-Policy",
                "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-src 'none';");
            await next();
        });

        _ = app.UseStaticFiles(new StaticFileOptions
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

        _ = app.UseRouting();
        _ = app.UseIdentityServer();

        _ = app.UseAuthentication();
        _ = app.UseAuthorization();

        _ = app.MapRazorPages()
            .RequireAuthorization();

        return app;
    }
}
