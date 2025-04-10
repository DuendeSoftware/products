// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Models;
using IdentityServerHost.Configuration;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerDb;

public class SeedData
{
    public static void EnsureSeedData(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();

        using (var context = scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>())
        {
            context.Database.Migrate();
        }

        using (var context = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>())
        {
            context.Database.Migrate();
            EnsureSeedData(context);
        }
    }

    private static void EnsureSeedData(ConfigurationDbContext context)
    {
        Console.WriteLine("Seeding database...");

        if (!context.Clients.Any())
        {
            Console.WriteLine("Clients being populated");
            foreach (var client in TestClients.Get())
            {
                context.Clients.Add(client.ToEntity());
            }
            context.SaveChanges();
        }
        else
        {
            Console.WriteLine("Clients already populated");
        }

        if (!context.IdentityResources.Any())
        {
            Console.WriteLine("IdentityResources being populated");
            foreach (var resource in TestResources.IdentityResources)
            {
                context.IdentityResources.Add(resource.ToEntity());
            }
            context.SaveChanges();
        }
        else
        {
            Console.WriteLine("IdentityResources already populated");
        }

        if (!context.ApiResources.Any())
        {
            Console.WriteLine("ApiResources being populated");
            foreach (var resource in TestResources.ApiResources)
            {
                context.ApiResources.Add(resource.ToEntity());
            }
            context.SaveChanges();
        }
        else
        {
            Console.WriteLine("ApiResources already populated");
        }

        if (!context.ApiScopes.Any())
        {
            Console.WriteLine("Scopes being populated");
            foreach (var resource in TestResources.ApiScopes)
            {
                context.ApiScopes.Add(resource.ToEntity());
            }
            context.SaveChanges();
        }
        else
        {
            Console.WriteLine("Scopes already populated");
        }

        if (!context.IdentityProviders.Any())
        {
            Console.WriteLine("OidcIdentityProviders being populated");
            context.IdentityProviders.Add(new OidcProvider
            {
                Scheme = "demoidsrv",
                DisplayName = "IdentityServer (Seeded)",
                Authority = "https://demo.duendesoftware.com",
                ClientId = "login",
            }.ToEntity());

            context.SaveChanges();
        }
        else
        {
            Console.WriteLine("OidcIdentityProviders already populated");
        }

        Console.WriteLine("Done seeding database.");
        Console.WriteLine();
    }
}
