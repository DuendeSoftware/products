using Microsoft.AspNetCore.DataProtection;
using Serilog;

namespace BffLocalApi;

internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        _ = builder.Services.AddRazorPages();

        _ = builder.Services.AddControllers();

        // add BFF services and server-side session management
        _ = builder.Services.AddBff()
            // if you wanted to enable a remote API (in addition or instead of the local API), then you could uncomment this line
            //.AddRemoteApis()
            .AddServerSideSessions();

        _ = builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = "cookie";
                options.DefaultChallengeScheme = "oidc";
                options.DefaultSignOutScheme = "oidc";
            })
            .AddCookie("cookie", options =>
            {
                options.Cookie.Name = "__Host-bff";
                options.Cookie.SameSite = SameSiteMode.Strict;
            })
            .AddOpenIdConnect("oidc", options =>
            {
                options.Authority = "https://demo.duendesoftware.com";
                options.ClientId = "interactive.confidential";
                options.ClientSecret = "secret";
                options.ResponseType = "code";
                options.ResponseMode = "query";

                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;
                options.MapInboundClaims = false;

                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("api");
                options.Scope.Add("offline_access");

                options.TokenValidationParameters.NameClaimType = "name";
                options.TokenValidationParameters.RoleClaimType = "role";
            });

        // Add `.PersistKeysTo…()` and `.ProtectKeysWith…()`calls
        // See more at https://docs.duendesoftware.com/general/data-protection
        _ = builder.Services.AddDataProtection()
            .SetApplicationName("BFF");

        return builder.Build();
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        _ = app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            _ = app.UseDeveloperExceptionPage();
        }

        _ = app.UseDefaultFiles();
        _ = app.UseStaticFiles();
        _ = app.UseAuthentication();
        _ = app.UseRouting();

        // add CSRF protection and status code handling for API endpoints
        _ = app.UseBff();
        _ = app.UseAuthorization();

        // local API endpoints
        _ = app.MapControllers()
            .RequireAuthorization()
            .AsBffApiEndpoint();

        app.MapBffManagementEndpoints();

        // if you wanted to enable a remote API (in addition or instead of the local API), then you could uncomment these lines
        //app.MapRemoteBffApiEndpoint("/remote", "https://api.your-server.com/api/test")
        //    .RequireAccessToken();

        return app;
    }
}
