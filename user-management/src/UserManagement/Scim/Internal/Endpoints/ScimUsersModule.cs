// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Modules;
using Duende.UserManagement.Membership.Internal;
using Duende.UserManagement.Profiles.Internal;
using Duende.UserManagement.Scim.Internal.Endpoints.Users;
using Duende.UserManagement.Scim.Internal.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

#pragma warning disable duende_experimental

namespace Duende.UserManagement.Scim.Internal.Endpoints;

internal sealed class ScimUsersHttpModule : IHttpModule
{
    public static void Register(IServiceCollection services)
    {
        services.RegisterModule<UserMembershipModule>();
        services.RegisterModule<UserProfilesModule>();

        // Register metadata/discovery endpoints (ServiceProviderConfig, ResourceTypes, Schemas)
        services.RegisterModule<ScimMetadataModule>();

        // Register endpoint handlers
        _ = services.AddScoped<ScimUserCommandProcessor>();
        _ = services.AddScoped<ScimGetUserEndpoint>();
        _ = services.AddScoped<ScimListUsersEndpoint>();
        _ = services.AddScoped<ScimSearchUsersEndpoint>();
        _ = services.AddScoped<ScimCreateUserEndpoint>();
        _ = services.AddScoped<ScimReplaceUserEndpoint>();
        _ = services.AddScoped<ScimPatchUserEndpoint>();
        _ = services.AddScoped<ScimDeleteUserEndpoint>();

        // Register SCIM endpoint options with defaults
        _ = services.AddOptions<ScimEndpointOptions>();
        _ = services.AddOptions<ScimOptions>();
    }

    public void MapEndpoints<T>(T app) where T : IEndpointRouteBuilder
    {
        var options = app.ServiceProvider.GetRequiredService<IOptions<ScimEndpointOptions>>().Value;
        var authOptions = app.ServiceProvider.GetRequiredService<IOptions<ScimOAuthOptions>>().Value;
        var customPolicy = string.IsNullOrWhiteSpace(authOptions.AuthorizationPolicyName) ? null : authOptions.AuthorizationPolicyName;
        var readPolicy = customPolicy ?? ScimConstants.ReadPolicyName;
        var writePolicy = customPolicy ?? ScimConstants.WritePolicyName;

        var group = app.MapGroup(options.Route);
        _ = group.AddEndpointFilter<ScimContentTypeFilter>();

        // GET /scim/Users — List/Filter
        _ = group.MapGet("", (
                [FromServices] ScimListUsersEndpoint endpoint,
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
            .WithName("SCIM List Users")
            .RequireAuthorization(readPolicy);

        // POST /scim/Users — Create
        _ = group.MapPost("", (
                [FromServices] ScimCreateUserEndpoint endpoint,
                ScimUserRequest? body,
                HttpContext ctx,
                Ct ct) =>
            endpoint.HandleAsync(body, ctx, ct))
            .WithName("SCIM Create User")
            .RequireAuthorization(writePolicy);

        // POST /scim/Users/.search — Search
        _ = group.MapPost(".search", (
                [FromServices] ScimSearchUsersEndpoint endpoint,
                ScimSearchRequest? body,
                HttpContext ctx,
                Ct ct) =>
            endpoint.HandleAsync(body, ctx, ct))
            .WithName("SCIM Search Users")
            .RequireAuthorization(readPolicy);

        // GET /scim/Users/{id} — Get single
        _ = group.MapGet("{id}", (
                [FromServices] ScimGetUserEndpoint endpoint,
                string id,
                HttpContext ctx,
                [FromQuery] string? attributes,
                [FromQuery] string? excludedAttributes,
                Ct ct) =>
            endpoint.HandleAsync(id, ctx, attributes, excludedAttributes, ct))
            .WithName("SCIM Get User")
            .RequireAuthorization(readPolicy);

        // PUT /scim/Users/{id} — Replace
        _ = group.MapPut("{id}", (
                [FromServices] ScimReplaceUserEndpoint endpoint,
                string id,
                ScimUserRequest? body,
                HttpContext ctx,
                Ct ct) =>
            endpoint.HandleAsync(id, body, ctx, ct))
            .WithName("SCIM Replace User")
            .RequireAuthorization(writePolicy);

        // PATCH /scim/Users/{id} — Patch
        _ = group.MapPatch("{id}", (
                [FromServices] ScimPatchUserEndpoint endpoint,
                string id,
                ScimPatchRequest? body,
                HttpContext ctx,
                Ct ct) =>
            endpoint.HandleAsync(id, body, ctx, ct))
            .WithName("SCIM Patch User")
            .RequireAuthorization(writePolicy);

        // DELETE /scim/Users/{id} — Delete
        _ = group.MapDelete("{id}", (
                [FromServices] ScimDeleteUserEndpoint endpoint,
                string id,
                HttpContext ctx,
                Ct ct) =>
            endpoint.HandleAsync(id, ctx, ct))
            .WithName("SCIM Delete User")
            .RequireAuthorization(writePolicy);
    }
}

#pragma warning restore duende_experimental
