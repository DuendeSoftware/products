// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace UserSessionDb;

public class Program
{
    public static void Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        using (var scope = host.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
        {
            using (var context = scope.ServiceProvider.GetService<SessionDbContext>())
            {
                Console.WriteLine("MIGRATING"); // TODO
                context.Database.Migrate();
            }
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
}
