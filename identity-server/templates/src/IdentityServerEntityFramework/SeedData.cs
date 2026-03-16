using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace IdentityServerHost;

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
        if (!context.Clients.Any())
        {
            Log.Debug("Clients being populated");
            foreach (var client in Config.Clients.ToList())
            {
                _ = context.Clients.Add(client.ToEntity());
            }
            _ = context.SaveChanges();
        }
        else
        {
            Log.Debug("Clients already populated");
        }

        if (!context.IdentityResources.Any())
        {
            Log.Debug("IdentityResources being populated");
            foreach (var resource in Config.IdentityResources.ToList())
            {
                _ = context.IdentityResources.Add(resource.ToEntity());
            }
            _ = context.SaveChanges();
        }
        else
        {
            Log.Debug("IdentityResources already populated");
        }

        if (!context.ApiScopes.Any())
        {
            Log.Debug("ApiScopes being populated");
            foreach (var resource in Config.ApiScopes.ToList())
            {
                _ = context.ApiScopes.Add(resource.ToEntity());
            }
            _ = context.SaveChanges();
        }
        else
        {
            Log.Debug("ApiScopes already populated");
        }

        if (!context.IdentityProviders.Any())
        {
            Log.Debug("OIDC IdentityProviders being populated");
            _ = context.IdentityProviders.Add(new OidcProvider
            {
                Scheme = "demoidsrv",
                DisplayName = "IdentityServer",
                Authority = "https://demo.duendesoftware.com",
                ClientId = "login",
            }.ToEntity());
            _ = context.SaveChanges();
        }
        else
        {
            Log.Debug("OIDC IdentityProviders already populated");
        }
    }
}
