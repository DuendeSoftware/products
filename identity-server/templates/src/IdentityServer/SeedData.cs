using Duende.IdentityServer;
using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace IdentityServerTemplate;

public class SeedData
{
    public static void EnsureSeedData(WebApplication app)
    {
        using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.Migrate();

            var context = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();
            context.Database.Migrate();
            EnsureSeedData(context);
        }
    }

    private static void EnsureSeedData(ConfigurationDbContext context)
    {
        SeedIdentityResources(context);
        SeedDynamicProviders(context);
    }

    private static void SeedIdentityResources(ConfigurationDbContext context)
    {
        if (!context.IdentityResources.Any())
        {
            Log.Debug("IdentityResources being populated");
            foreach (var resource in Config.IdentityResources.ToList())
            {
                context.IdentityResources.Add(resource.ToEntity());
            }
            context.SaveChanges();
        }
        else
        {
            Log.Debug("IdentityResources already populated");
        }
    }

    private static void SeedDynamicProviders(ConfigurationDbContext context)
    {
        if (!context.IdentityProviders.Any())
        {
            Log.Debug("IdentityProviders being populated...");

            var duendeDemoProvider = new OidcProvider
            {
                Scheme = "oidc-demo",
                DisplayName = "Duende Demo",
                Authority = "https://demo.duendesoftware.com",
                ClientId = "interactive.confidential",
                ClientSecret = "secret",
                ResponseType = "code",
                Scope = "openid profile",
                GetClaimsFromUserInfoEndpoint = true

                //options.SignInScheme = IdentityServerConstants.ExternalCookieAuthenticationScheme;
                //options.SignOutScheme = IdentityServerConstants.SignoutScheme;
                //options.SaveTokens = true;

                //     options.TokenValidationParameters = new TokenValidationParameters
                //     {
                //     NameClaimType = "name",
                //     RoleClaimType = "role"
                // };
            };
            duendeDemoProvider.Properties["IconUrl"] = "/img/duende-logo.svg";

            context.IdentityProviders.Add(duendeDemoProvider.ToEntity());

            context.SaveChanges();

            Log.Debug("IdentityProviders populated.");
        }
        else
        {
            Log.Debug("OidcIdentityProviders already populated");
        }
    }
}
