// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.IntegrationTests.TestFramework;

/// <summary>
/// Records the exception for the last request, whether it was handled by exception handling
/// middleware or escaped the pipeline. An escaped exception is turned into a 500 response.
/// </summary>
public sealed class ExceptionRecorder : IStartupFilter
{
    public Exception? Exception { get; private set; }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (HttpContext context, RequestDelegate nextMiddleware) =>
        {
            try
            {
                await nextMiddleware(context);
                Exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
            }
            catch (Exception ex)
            {
                Exception = ex;
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            }
        });
        next(app);
    };
}
