// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;

namespace IdentityServerDb;

internal class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var cn = builder.Configuration.GetConnectionString("DefaultConnection");

        builder.Services.AddOperationalDbContext(options =>
        {
            options.ConfigureDbContext = b =>
                b.UseSqlServer(cn, dbOpts => dbOpts.MigrationsAssembly(typeof(Program).Assembly.FullName));
        });

        builder.Services.AddConfigurationDbContext(options =>
        {
            options.ConfigureDbContext = b =>
                b.UseSqlServer(cn, dbOpts => dbOpts.MigrationsAssembly(typeof(Program).Assembly.FullName));
        });

        var app = builder.Build();

        SeedData.EnsureSeedData(app.Services);

        // Exit the application
        Console.WriteLine("Exiting application...");
        Environment.Exit(0);
    }
}
