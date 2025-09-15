// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace UserSessionDb;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var cn = builder.Configuration.GetConnectionString("db");

        builder.Services.AddDbContext<SessionDbContext>(options =>
        {
            //options.UseSqlServer(cn, dbOpts => dbOpts.MigrationsAssembly(typeof(Program).Assembly.FullName));
            options.UseSqlite(cn, dbOpts => dbOpts.MigrationsAssembly(typeof(Program).Assembly.FullName));
        });

        var app = builder.Build();

        using (var scope = app.Services.GetRequiredService<IServiceScopeFactory>().CreateScope())
        {
            using (var context = scope.ServiceProvider.GetService<SessionDbContext>())
            {
                Console.WriteLine("MIGRATING"); // TODO
                context.Database.Migrate();
            }
        }
    }
}
