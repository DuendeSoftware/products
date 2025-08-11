using System.IdentityModel.Tokens.Jwt;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace TemplateWebApp;

internal record IdentityProviderConfiguration(string Authority, string ClientId, string ClientSecret, string Scopes);

internal static class HostingExtensions
{
    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddRazorPages();

        // In production, these values should be stored securely in Azure Key Vault or AWS Secrets Manager.
        var identityProviderConfig = builder.Configuration
            .GetSection("IdentityProvider")
            .Get<IdentityProviderConfiguration>()

            ?? throw new InvalidOperationException("IdentityProvider configuration is missing or invalid.");

        JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

        // The __Host- prefix ensures the cookie is host-only, requires Secure and Path=/ attributes
        var hostCookiePrefix = "__Host-";

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
            .AddCookie(options =>
            {
                options.Cookie.Name = hostCookiePrefix + "TemplateWebApp";
                options.Cookie.Path = "/";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            })
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.Events.OnRemoteFailure = context =>
                {
                    context.Response.Redirect("/Error?message=" + context.Failure?.Message ?? "A remote failure occured");
                    context.HandleResponse();
                    return Task.CompletedTask;
                };

                options.Authority = identityProviderConfig.Authority;
                options.ClientId = identityProviderConfig.ClientId;
                options.ClientSecret = identityProviderConfig.ClientSecret;

                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;

                options.Scope.Clear();
                options.Scope.Add("offline_access");

                // Add additional scopes from the configuration
                identityProviderConfig.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .ToList().ForEach(scope => options.Scope.Add(scope));

                options.GetClaimsFromUserInfoEndpoint = true;
                options.SaveTokens = true;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = JwtClaimTypes.Name,
                    RoleClaimType = JwtClaimTypes.Role,
                };

                options.DisableTelemetry = true;
            });

        // Uncomment to set up Duende.AccessTokenManagement to automatically manage access tokens for API calls.
        // builder.Services.AddOpenIdConnectAccessTokenManagement();
        // builder.Services.AddUserAccessTokenHttpClient(name: "client", configureClient: client =>
        // {
        //     client.BaseAddress = new Uri("https://example.com/api/");
        // });

        // In production, use a secure location for storing keys, such as Azure Key Vault or AWS Secrets Manager.
        // Make sure to secure the keys at rest.
        builder.Services.AddDataProtection()
            .SetApplicationName("TemplateWebApp")
            .PersistKeysToFileSystem(new DirectoryInfo("./keys"));

        return builder.Build();
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapStaticAssets();
        app.MapRazorPages()
           .WithStaticAssets();

        return app;
    }
}
