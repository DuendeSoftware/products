using Duende.Bff.Yarp;
using Serilog;

namespace BffLocalApi;

internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddRazorPages();

        builder.Services.AddControllers();

        // add BFF services and server-side session management
        builder.Services.AddBff()
            // if you wanted to enable a remote API (in addition or instead of the local API), then you could uncomment this line
            //.AddRemoteApis()
            .AddServerSideSessions();

        builder.Services.AddAuthentication(options =>
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

        return builder.Build();
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseRouting();

        // add CSRF protection and status code handling for API endpoints
        app.UseBff();
        app.UseAuthorization();
        
        // local API endpoints
        app.MapControllers()
            .RequireAuthorization()
            .AsBffApiEndpoint();

        app.MapBffManagementEndpoints();

        // if you wanted to enable a remote API (in addition or instead of the local API), then you could uncomment these lines
        //app.MapRemoteBffApiEndpoint("/remote", "https://api.your-server.com/api/test")
        //    .RequireAccessToken();

        return app;
    }
}