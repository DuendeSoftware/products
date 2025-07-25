// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using IdentityServerHost.Data;
using IdentityServerHost.Models;
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

        services.Configure<IdentityOptions>(opt =>
        {
            // Complexity requirements are not actually helpful, but the length should be >>3 in practice. This is done
            // for demo purposes only (user bob/bob).
            opt.Password.RequireDigit = false;
            opt.Password.RequireLowercase = false;
            opt.Password.RequireUppercase = false;
            opt.Password.RequireNonAlphanumeric = false;
            opt.Password.RequiredLength = 3;
        });
    }

    public void Configure(IApplicationBuilder app)
    {
    }
}
