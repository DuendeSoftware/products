// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.Bff.Tests.TestFramework;

namespace Duende.Bff.Tests.TestInfra;

public class ApiHost : TestHost
{
    public HttpStatusCode? ApiStatusCodeToReturn { get; set; }


    public ApiHost(TestHostContext context, IdentityServerTestHost identityServerUri) : base(context, new Uri("https://api"))
    {
        OnConfigureServices += services =>
        {
            services.AddAuthentication("token")
                .AddJwtBearer("token", options =>
                {
                    options.Authority = identityServerUri.Url().ToString();
                    options.Audience = identityServerUri.Url("/resources").ToString();
                    options.MapInboundClaims = false;
                    options.BackchannelHttpHandler = identityServerUri.Server.CreateHandler();
                });
        };

        OnConfigure += app =>
        {
            app.UseRouting();

            app.UseAuthentication();
            // adds authorization for local and remote API endpoints
            app.UseAuthorization();
        };

        OnConfigureEndpoints += endpoints =>
        {
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

                context.Response.StatusCode = ApiStatusCodeToReturn == null
                    ? 200
                    : (int)ApiStatusCodeToReturn;

                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            });
        };
    }
}
