// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Hosts.Shared.Configuration;
using Duende.IdentityServer.UI.AspNetIdentity.Models;
using IdentityServerHost.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AspNetIdentityDb;

public class SeedData
{
    public static void EnsureSeedData(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
        dbContext.Database.Migrate();

        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var testUser in TestUsers.Users)
        {
            var userInDb = userMgr.FindByNameAsync(testUser.Username).Result;
            if (userInDb is not null)
            {
                Console.WriteLine($"{testUser.Username} already exists");
                continue;
            }

            userInDb = new ApplicationUser
            {
                UserName = testUser.Username
            };

            var result = userMgr.CreateAsync(userInDb, testUser.Password).Result;
            if (!result.Succeeded)
            {
                throw new Exception(result.Errors.First().Description);
            }

            result = userMgr.AddClaimsAsync(userInDb, testUser.Claims).Result;
            if (!result.Succeeded)
            {
                throw new Exception(result.Errors.First().Description);
            }

            Console.WriteLine($"{testUser.Username} created");
        }
    }
}
