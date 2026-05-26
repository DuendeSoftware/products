// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Modules;
using Duende.UserManagement.Scim.Internal.Endpoints.Bulk;
using Duende.UserManagement.Scim.Internal.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Scim.Internal.Endpoints;

internal sealed class ScimBulkModule : IHttpModule
{
    public static void Register(IServiceCollection services) =>
        services.AddScoped<ScimBulkEndpoint>();

    public void MapEndpoints<T>(T app) where T : IEndpointRouteBuilder
    {
        var options = app.ServiceProvider.GetRequiredService<IOptions<ScimEndpointOptions>>().Value;

        var group = app.MapGroup(options.BulkRoute);
        _ = group.AddEndpointFilter<ScimContentTypeFilter>();

        _ = group.MapPost("", (
                [FromServices] ScimBulkEndpoint endpoint,
                ScimBulkRequest? body,
                HttpContext ctx,
                Ct ct) =>
            endpoint.HandleAsync(body, ctx, ct))
            .WithName("SCIM Bulk");
    }
}
