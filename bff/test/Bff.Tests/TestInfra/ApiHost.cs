// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.Bff.Tests.TestFramework;
using Microsoft.AspNetCore.Authentication;

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

        OnConfigureApp += app =>
        {
            app.Map("/{**catch-all}",
                async context =>
                {
                    await ReturnApiCallDetails(context, () => ApiStatusCodeToReturn ?? HttpStatusCode.OK);
                });
        };
    }

    protected override void ConfigureApp(IApplicationBuilder app)
    {
        app.UseRouting();

        app.UseAuthentication();
        // adds authorization for local and remote API endpoints
        app.UseAuthorization();
        base.ConfigureApp(app);
    }

    public static async Task ReturnApiCallDetails(HttpContext context, Func<HttpStatusCode>? LocalApiResponseStatus = null)
    {
        LocalApiResponseStatus ??= () => HttpStatusCode.OK;
        var sub = context.User.FindFirst("sub")?.Value;
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
            HttpMethod.Parse(context.Request.Method),
            context.Request.Path.Value ?? "/",
            sub,
            context.User.FindFirst("client_id")?.Value,
            context.User.Claims.Select(x => new TestClaimRecord(x.Type, x.Value)).ToArray())
        {
            Body = body,
            RequestHeaders = requestHeaders
        };

        if (LocalApiResponseStatus() == HttpStatusCode.OK)
        {
            context.Response.StatusCode = 200;

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
        else if (LocalApiResponseStatus() == HttpStatusCode.Unauthorized)
        {
            await context.ChallengeAsync();
        }
        else if (LocalApiResponseStatus() == HttpStatusCode.Forbidden)
        {
            await context.ForbidAsync();
        }
        else
        {
            throw new InvalidOperationException("Invalid LocalApiResponseStatus");
        }
    }
}
