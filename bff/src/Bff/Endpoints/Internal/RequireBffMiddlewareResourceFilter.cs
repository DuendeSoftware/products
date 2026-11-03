// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Endpoints.Internal;

internal sealed class RequireBffMiddlewareResourceFilter(IOptions<BffOptions> options) : IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<IBffApiMetadata>() != null)
        {
            context.HttpContext.CheckForBffMiddleware(options.Value);
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}