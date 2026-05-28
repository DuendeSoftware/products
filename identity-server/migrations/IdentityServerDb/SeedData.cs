// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Duende.IdentityServer.Hosts.Shared.Configuration;
using Duende.IdentityServer.Models;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerDb;

public class SeedData
{
    public static void EnsureSeedData(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        using (var context = scope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>())
        {
            context.Database.Migrate();
        }

        using (var context = scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>())
        {
            context.Database.Migrate();
            EnsureSeedData(context, configuration);
        }
    }

    private static void EnsureSeedData(ConfigurationDbContext context, IConfiguration configuration)
    {
        Console.WriteLine("Seeding database...");

        if (!context.Clients.Any())
        {
            Console.WriteLine("Clients being populated");
            foreach (var client in TestClients.Get())
            {
                _ = context.Clients.Add(client.ToEntity());
            }
            _ = context.SaveChanges();
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
                _ = context.IdentityResources.Add(resource.ToEntity());
            }
            _ = context.SaveChanges();
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
                _ = context.ApiResources.Add(resource.ToEntity());
            }
            _ = context.SaveChanges();
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
                _ = context.ApiScopes.Add(resource.ToEntity());
            }
            _ = context.SaveChanges();
        }
        else
        {
            Console.WriteLine("Scopes already populated");
        }

        if (!context.IdentityProviders.Any())
        {
            Console.WriteLine("IdentityProviders being populated");
            _ = context.IdentityProviders.Add(new OidcProvider
            {
                Scheme = "demoidsrv",
                DisplayName = "IdentityServer (Seeded)",
                Authority = "https://demo.duendesoftware.com",
                ClientId = "login",
            }.ToEntity());

            // Seed a WS-Federation provider to demonstrate custom IdentityProvider types.
            // The WsFedProvider derived type is defined in the host projects; here we seed
            // the raw entity with the "wsfed" type and properties in the dictionary.
            // Configure WsFed:MetadataAddress and WsFed:Wtrealm via user secrets.
            // See identity-server/hosts/EntityFramework10/README.md for setup instructions.
            var wsFedMetadata = configuration["WsFed:MetadataAddress"] ?? "https://login.microsoftonline.com/{tenant-id}/federationmetadata/2007-06/federationmetadata.xml";
            var wsFedWtrealm = configuration["WsFed:Wtrealm"] ?? "api://{client-id}";
            _ = context.IdentityProviders.Add(new Duende.IdentityServer.EntityFramework.Entities.IdentityProvider
            {
                Scheme = "dynamicprovider-entra-wsfed",
                DisplayName = "Entra ID (via WS-Fed Dynamic Provider)",
                Type = "wsfed",
                Enabled = true,
                Properties = $"{{\"MetadataAddress\":\"{wsFedMetadata}\",\"Wtrealm\":\"{wsFedWtrealm}\"}}"
            });

            _ = context.SaveChanges();
        }
        else
        {
            Console.WriteLine("IdentityProviders already populated");
        }

        Console.WriteLine("Done seeding database.");
        Console.WriteLine();
    }
}
