// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable 1591

namespace Duende.IdentityServer.Hosting;

public class BaseUrlMiddleware
{
    private readonly RequestDelegate _next;

    public BaseUrlMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        context.RequestServices.GetRequiredService<IServerUrls>().BasePath = context.Request.PathBase.Value;

        await _next(context);
    }
}
