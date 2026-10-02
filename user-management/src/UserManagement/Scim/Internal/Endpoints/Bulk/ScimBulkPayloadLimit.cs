// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Scim.Internal.Endpoints.Bulk;

internal static class ScimBulkPayloadLimit
{
    internal static void Apply(RouteHandlerBuilder endpoint) =>
        endpoint.Finally(builder =>
        {
            var next = builder.RequestDelegate ??
                throw new InvalidOperationException("The SCIM bulk request delegate has not been created.");

            builder.RequestDelegate = context => EnforceAsync(context, next);
        });

    internal static async Task EnforceAsync(HttpContext context, RequestDelegate next)
    {
        var maxPayloadSize = context.RequestServices
            .GetRequiredService<IOptions<ScimOptions>>()
            .Value
            .MaxBulkPayloadSize;

        if (maxPayloadSize < 0 ||
            context.Request.ContentLength is { } contentLength && contentLength > maxPayloadSize)
        {
            await WritePayloadTooLargeAsync(context, maxPayloadSize);
            return;
        }

        var originalBody = context.Request.Body;
        var originalBodyPipeFeature = context.Features.Get<IRequestBodyPipeFeature>();
        var limitedBody = new ScimBulkReadStream(originalBody, maxPayloadSize, leaveOpen: true);

        context.Request.Body = limitedBody;
        context.Features.Set<IRequestBodyPipeFeature>(new RequestBodyPipeFeature(context));

        try
        {
            await next(context);
            if (limitedBody.LimitExceeded && !context.Response.HasStarted)
            {
                await WritePayloadTooLargeAsync(context, maxPayloadSize);
            }
        }
        catch (InvalidOperationException ex) when (
            ScimBulkReadStream.IsPayloadTooLarge(ex) &&
            !context.Response.HasStarted)
        {
            await WritePayloadTooLargeAsync(context, maxPayloadSize);
        }
        finally
        {
            context.Request.Body = originalBody;
            context.Features.Set(originalBodyPipeFeature);
        }
    }

    private static Task WritePayloadTooLargeAsync(HttpContext context, int maxPayloadSize) =>
        ScimResults.Error(
            StatusCodes.Status413PayloadTooLarge,
            $"The size of the bulk operation exceeds the maxPayloadSize ({maxPayloadSize}).")
        .ExecuteAsync(context);
}
