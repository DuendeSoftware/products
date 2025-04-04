// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace UserSessionDb;

public class Startup
{
    public IConfiguration Configuration { get; }

    public Startup(IConfiguration config) => Configuration = config;

    public void ConfigureServices(IServiceCollection services)
    {
        var cn = Configuration.GetConnectionString("db");

        services.AddDbContext<SessionDbContext>(options =>
        {
            //options.UseSqlServer(cn, dbOpts => dbOpts.MigrationsAssembly(typeof(Startup).Assembly.FullName));
            options.UseSqlite(cn, dbOpts => dbOpts.MigrationsAssembly(typeof(Startup).Assembly.FullName));
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
    }
}
