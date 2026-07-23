// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Modules;
using Duende.UserManagement.Scim.Internal.Endpoints.Groups;
using Duende.UserManagement.Scim.Internal.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

#pragma warning disable duende_experimental

namespace Duende.UserManagement.Scim.Internal.Endpoints;

internal sealed class ScimGroupsModule : IHttpModule
{
    public static void Register(IServiceCollection services)
    {
        // Register endpoint handlers
        _ = services.AddScoped<ScimGroupCommandProcessor>();
        _ = services.AddScoped<ScimGetGroupEndpoint>();
        _ = services.AddScoped<ScimListGroupsEndpoint>();
        _ = services.AddScoped<ScimSearchGroupsEndpoint>();
        _ = services.AddScoped<ScimCreateGroupEndpoint>();
        _ = services.AddScoped<ScimReplaceGroupEndpoint>();
        _ = services.AddScoped<ScimPatchGroupEndpoint>();
        _ = services.AddScoped<ScimDeleteGroupEndpoint>();
    }

    public void MapEndpoints<T>(T app) where T : IEndpointRouteBuilder
    {
        var options = app.ServiceProvider.GetRequiredService<IOptions<ScimEndpointOptions>>().Value;
        var authOptions = app.ServiceProvider.GetRequiredService<IOptions<ScimOAuthOptions>>().Value;
        var customPolicy = string.IsNullOrWhiteSpace(authOptions.AuthorizationPolicyName) ? null : authOptions.AuthorizationPolicyName;
        var readPolicy = customPolicy ?? ScimConstants.ReadPolicyName;
        var writePolicy = customPolicy ?? ScimConstants.WritePolicyName;

        var group = app.MapGroup(options.GroupsRoute);
        _ = group.AddEndpointFilter<ScimContentTypeFilter>();

        // GET /scim/Groups — List/Filter
        _ = group.MapGet("", (
                [FromServices] ScimListGroupsEndpoint endpoint,
                HttpContext ctx,
                [FromQuery] string? filter,
                [FromQuery] int? startIndex,
                [FromQuery] int? count,
                [FromQuery] string? sortBy,
                [FromQuery] string? sortOrder,
                [FromQuery] string? attributes,
                [FromQuery] string? excludedAttributes,
                Ct ct) =>
            endpoint.HandleAsync(ctx, filter, startIndex, count, sortBy, sortOrder, attributes, excludedAttributes, ct))
            .WithName("SCIM List Groups")
            .RequireAuthorization(readPolicy);

        // POST /scim/Groups — Create
        _ = group.MapPost("", (
                [FromServices] ScimCreateGroupEndpoint endpoint,
                ScimGroupRequest? body,
                HttpContext ctx,
                Ct ct) =>
            endpoint.HandleAsync(body, ctx, ct))
            .WithName("SCIM Create Group")
            .RequireAuthorization(writePolicy);

        // POST /scim/Groups/.search — Search
        _ = group.MapPost(".search", (
                [FromServices] ScimSearchGroupsEndpoint endpoint,
                ScimSearchRequest? body,
                HttpContext ctx,
                Ct ct) =>
            endpoint.HandleAsync(body, ctx, ct))
            .WithName("SCIM Search Groups")
            .RequireAuthorization(readPolicy);

        // GET /scim/Groups/{id} — Get single
        _ = group.MapGet("{id}", (
                [FromServices] ScimGetGroupEndpoint endpoint,
                string id,
                HttpContext ctx,
                [FromQuery] string? attributes,
                [FromQuery] string? excludedAttributes,
                Ct ct) =>
            endpoint.HandleAsync(id, ctx, attributes, excludedAttributes, ct))
            .WithName("SCIM Get Group")
            .RequireAuthorization(readPolicy);

        // PUT /scim/Groups/{id} — Replace
        _ = group.MapPut("{id}", (
                [FromServices] ScimReplaceGroupEndpoint endpoint,
                string id,
                ScimGroupRequest? body,
                HttpContext ctx,
                Ct ct) =>
            endpoint.HandleAsync(id, body, ctx, ct))
            .WithName("SCIM Replace Group")
            .RequireAuthorization(writePolicy);

        // PATCH /scim/Groups/{id} — Patch
        _ = group.MapPatch("{id}", (
                [FromServices] ScimPatchGroupEndpoint endpoint,
                string id,
                ScimPatchRequest? body,
                HttpContext ctx,
                Ct ct) =>
            endpoint.HandleAsync(id, body, ctx, ct))
            .WithName("SCIM Patch Group")
            .RequireAuthorization(writePolicy);

        // DELETE /scim/Groups/{id} — Delete
        _ = group.MapDelete("{id}", (
                [FromServices] ScimDeleteGroupEndpoint endpoint,
                string id,
                HttpContext ctx,
                Ct ct) =>
            endpoint.HandleAsync(id, ctx, ct))
            .WithName("SCIM Delete Group")
            .RequireAuthorization(writePolicy);
    }
}

#pragma warning restore duende_experimental
