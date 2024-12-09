// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Duende.IdentityServer.Services;

#pragma warning disable 1591

namespace Duende.IdentityServer.Hosting;

public class BaseUrlMiddleware
{
    private readonly RequestDelegate _next;

    public BaseUrlMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task Invoke(HttpContext context)
    {
        context.RequestServices.GetRequiredService<IServerUrls>().BasePath = context.Request.PathBase.Value;

        return _next(context);
    }
}