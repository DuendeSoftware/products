// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace Api
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            
            services.AddAuthentication("token")
                .AddJwtBearer("token", options =>
                {
                    options.Authority = "https://localhost:5001";
                    options.MapInboundClaims = false;

                    options.TokenValidationParameters = new TokenValidationParameters()
                    {
                        ValidateAudience = false,
                        ValidTypes = new[] { "at+jwt" },
                        
                        NameClaimType = "name",
                        RoleClaimType = "role"
                    };
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("ApiCaller", policy =>
                {
                    policy.RequireClaim("scope", "api");
                });
                
                options.AddPolicy("RequireInteractiveUser", policy =>
                {
                    policy.RequireClaim("sub");
                });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
            });
            
            app.UseSerilogRequestLogging();
            
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers()
                    .RequireAuthorization("ApiCaller");
            });
        }
    }
}