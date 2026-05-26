// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Modules;
using Duende.UserManagement.Scim.Internal.Endpoints.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Scim.Internal;

/// <summary>
/// HTTP module that registers the SCIM metadata/discovery endpoints:
/// ServiceProviderConfig, ResourceTypes, and Schemas (RFC 7644 §4).
/// </summary>
internal sealed class ScimMetadataModule : IHttpModule
{
    public static void Register(IServiceCollection services)
    {
        // Register the capability resolver (shared across metadata endpoints)
        _ = services.AddScoped<ScimCapabilityResolver>();

        // Register the default schema mapper if no custom one has been registered
        services.TryAddSingleton<IScimSchemaMapper, DefaultScimSchemaMapper>();

        // Register endpoint handlers
        _ = services.AddScoped<ServiceProviderConfigEndpoint>();
        _ = services.AddScoped<ResourceTypesEndpoint>();
        _ = services.AddScoped<SchemasEndpoint>();
    }

    public void MapEndpoints<T>(T app) where T : IEndpointRouteBuilder
    {
        var options = app.ServiceProvider.GetRequiredService<IOptions<ScimEndpointOptions>>().Value;

        var group = app.MapGroup(options.MetadataRoute);

        // GET /scim/ServiceProviderConfig
        _ = group.MapGet("ServiceProviderConfig", (
                [FromServices] ServiceProviderConfigEndpoint endpoint,
                HttpContext ctx) =>
            endpoint.Handle(ctx))
            .WithName("SCIM ServiceProviderConfig");

        // GET /scim/ResourceTypes
        _ = group.MapGet("ResourceTypes", (
                [FromServices] ResourceTypesEndpoint endpoint,
                HttpContext ctx) =>
            endpoint.HandleList(ctx))
            .WithName("SCIM List ResourceTypes");

        // GET /scim/ResourceTypes/{id}
        _ = group.MapGet("ResourceTypes/{id}", (
                [FromServices] ResourceTypesEndpoint endpoint,
                string id,
                HttpContext ctx) =>
            endpoint.HandleGet(id, ctx))
            .WithName("SCIM Get ResourceType");

        // GET /scim/Schemas
        _ = group.MapGet("Schemas", (
                [FromServices] SchemasEndpoint endpoint,
                HttpContext ctx,
                Ct ct) =>
            endpoint.HandleListAsync(ctx, ct))
            .WithName("SCIM List Schemas");

        // GET /scim/Schemas/{id}
        _ = group.MapGet("Schemas/{id}", (
                [FromServices] SchemasEndpoint endpoint,
                string id,
                HttpContext ctx,
                Ct ct) =>
            endpoint.HandleGetAsync(id, ctx, ct))
            .WithName("SCIM Get Schema");
    }
}
