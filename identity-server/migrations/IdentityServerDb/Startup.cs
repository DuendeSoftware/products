// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.EntityFramework.Storage;
using Microsoft.EntityFrameworkCore;

namespace SqlServer;

public class Startup
{
    public IConfiguration Configuration { get; }

    public Startup(IConfiguration config) => Configuration = config;

    public void ConfigureServices(IServiceCollection services)
    {
        var cn = Configuration.GetConnectionString("DefaultConnection");

        services.AddOperationalDbContext(options =>
        {
            options.ConfigureDbContext = b =>
                b.UseSqlServer(cn, dbOpts => dbOpts.MigrationsAssembly(typeof(Startup).Assembly.FullName));
        });

        services.AddConfigurationDbContext(options =>
        {
            options.ConfigureDbContext = b =>
                b.UseSqlServer(cn, dbOpts => dbOpts.MigrationsAssembly(typeof(Startup).Assembly.FullName));
        });
    }

    public void Configure(IApplicationBuilder app)
    {
    }
}
