// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Bff.Tests.TestFramework;
using Duende.IdentityServer.Models;

namespace Duende.Bff.Tests.TestHosts;

public class ApiHost : GenericHost
{
    public int? ApiStatusCodeToReturn { get; set; }

    private readonly IdentityServerHost _identityServerHost;

    public ApiHost(
        WriteTestOutput output,
        IdentityServerHost identityServerHost,
        string scope,
        string baseAddress = "https://api")
        : base(output, baseAddress)
    {
        _identityServerHost = identityServerHost;

        _identityServerHost.ApiScopes.Add(new ApiScope(scope));

        OnConfigureServices += ConfigureServices;
        OnConfigure += Configure;
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddRouting();
        services.AddAuthorization();

        services.AddAuthentication("token")
            .AddJwtBearer("token", options =>
            {
                options.Authority = _identityServerHost.Url();
                options.Audience = _identityServerHost.Url("/resources");
                options.MapInboundClaims = false;
                options.BackchannelHttpHandler = _identityServerHost.Server.CreateHandler();
            });
    }

    private void Configure(IApplicationBuilder app)
    {
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.Map("/return_unauthenticated",
                context =>
                {
                    context.Response.StatusCode = (int)System.Net.HttpStatusCode.Unauthorized;
                    return Task.CompletedTask;
                });

            endpoints.Map("/return_forbidden",
                context =>
                {
                    context.Response.StatusCode = (int)System.Net.HttpStatusCode.Forbidden;
                    return Task.CompletedTask;
                });

            endpoints.Map("/{**catch-all}", async context =>
            {
                // capture body if present
                var body = default(string);
                if (context.Request.HasJsonContentType())
                {
                    using (var sr = new StreamReader(context.Request.Body))
                    {
                        body = await sr.ReadToEndAsync();
                    }
                }

                // capture request headers
                var requestHeaders = new Dictionary<string, List<string>>();
                foreach (var header in context.Request.Headers)
                {
                    var values = new List<string>(header.Value.Select(v => v ?? string.Empty));
                    requestHeaders.Add(header.Key, values);
                }

                var response = new ApiCallDetails(
                    Method: HttpMethod.Parse(context.Request.Method),
                    Path: context.Request.Path.Value ?? "/",
                    Sub: context.User.FindFirst("sub")?.Value,
                    ClientId: context.User.FindFirst("client_id")?.Value,
                    Claims: context.User.Claims.Select(x => new TestClaimRecord(x.Type, x.Value)).ToArray())
                {
                    Body = body,
                    RequestHeaders = requestHeaders
                };

                context.Response.StatusCode = ApiStatusCodeToReturn ?? 200;
                ApiStatusCodeToReturn = null;

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            });
        });
    }
}
