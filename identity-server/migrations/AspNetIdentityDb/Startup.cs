// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.UI.AspNetIdentity.Models;
using IdentityServerHost.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AspNetIdentityDb;

public class Startup
{
    public IConfiguration Configuration { get; }

    public Startup(IConfiguration config) => Configuration = config;

    public void ConfigureServices(IServiceCollection services)
    {
        var cn = Configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseSqlServer(cn, dbOpts => dbOpts.MigrationsAssembly(typeof(Startup).Assembly.FullName));
        });

        services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();
    }

    public void Configure(IApplicationBuilder app)
    {
    }
}
