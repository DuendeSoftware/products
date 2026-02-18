// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.IdentityServer.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

internal class RedirectResult(Uri RedirectUri) : IEndpointResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<RedirectResult>>();
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(RedirectUri);

        logger.Redirecting(LogLevel.Trace, RedirectUri);

        httpContext.Response.StatusCode = (int)HttpStatusCode.Redirect;
        httpContext.Response.Headers.Location = RedirectUri.ToString();

        return Task.CompletedTask;
    }
}

internal class ValidationProblemResult(string title, params KeyValuePair<string, string[]>[] errors) : IEndpointResult
{
    public async Task ExecuteAsync(HttpContext context) =>
        await Results.ValidationProblem(new Dictionary<string, string[]>(errors), title).ExecuteAsync(context);
}
