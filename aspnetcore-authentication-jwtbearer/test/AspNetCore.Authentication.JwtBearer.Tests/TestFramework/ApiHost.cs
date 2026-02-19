// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
namespace Duende.AspNetCore.Authentication.JwtBearer.TestFramework;

public class ApiHost : GenericHost
{
    public const string AuthenticationScheme = "token";

    public int? ApiStatusCodeToReturn { get; set; }

    private readonly IdentityServerHost _identityServerHost;
    public event Action<HttpContext> ApiInvoked = ctx => { };

    public ApiHost(IdentityServerHost identityServerHost, string baseAddress = "https://api")
        : base(baseAddress)
    {
        _identityServerHost = identityServerHost;

        OnConfigureServices += ConfigureServices;
        OnConfigure += Configure;
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddHybridCache();
        services.AddRouting();
        services.AddAuthorization();

        services.AddAuthentication(AuthenticationScheme)
            .AddJwtBearer(AuthenticationScheme, options =>
            {
                options.Authority = _identityServerHost.Url();
                options.Audience = _identityServerHost.Url("/resources");
                options.MapInboundClaims = false;
                options.BackchannelHttpHandler = _identityServerHost.Server.CreateHandler();
            });
    }

    private void Configure(IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            ApiInvoked.Invoke(context);
            if (ApiStatusCodeToReturn != null)
            {
                context.Response.StatusCode = ApiStatusCodeToReturn.Value;
                ApiStatusCodeToReturn = null;
                return;
            }

            await next();
        });

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();
    }
}
