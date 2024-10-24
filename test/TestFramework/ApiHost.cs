// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Duende.AspNetCore.TestFramework;

public class ApiHost : GenericHost
{
    public const string AuthenticationScheme = "token";

    public int? ApiStatusCodeToReturn { get; set; }

    private readonly IdentityServerHost _identityServerHost;
    public event Action<HttpContext> ApiInvoked = ctx => { };
    
    public ApiHost(IdentityServerHost identityServerHost, ITestOutputHelper testOutputHelper, string baseAddress = "https://api") 
        : base(testOutputHelper, baseAddress)
    {
        _identityServerHost = identityServerHost;

        OnConfigureServices += ConfigureServices;
        OnConfigure += Configure;
    }

    private void ConfigureServices(IServiceCollection services)
    {
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
        app.Use(async(context, next) => 
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

        // app.UseEndpoints(endpoints =>
        // {
        //     // endpoints.Map("/{**catch-all}", async context =>
        //     // {
        //     //     // capture body if present
        //     //     var body = default(string);
        //     //     if (context.Request.HasJsonContentType())
        //     //     {
        //     //         using (var sr = new StreamReader(context.Request.Body))
        //     //         {
        //     //             body = await sr.ReadToEndAsync();
        //     //         }
        //     //     }
        //     //     
        //     //     // capture request headers
        //     //     var requestHeaders = new Dictionary<string, List<string>>();
        //     //     foreach (var header in context.Request.Headers)
        //     //     {
        //     //         var values = new List<string>(header.Value.Select(v => v));
        //     //         requestHeaders.Add(header.Key, values);
        //     //     }
        //     //
        //     //     var response = new ApiResponse(
        //     //         context.Request.Method,
        //     //         context.Request.Path.Value,
        //     //         context.User.FindFirst(("sub"))?.Value,
        //     //         context.User.FindFirst(("client_id"))?.Value,
        //     //         context.User.Claims.Select(x => new ClaimRecord(x.Type, x.Value)).ToArray())
        //     //     {
        //     //         Body = body,
        //     //         RequestHeaders = requestHeaders
        //     //     };
        //     //
        //     //     context.Response.StatusCode = ApiStatusCodeToReturn ?? 200;
        //     //     ApiStatusCodeToReturn = null;
        //     //
        //     //     context.Response.ContentType = "application/json";
        //     //     await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        //     // });
        // });
    }
}