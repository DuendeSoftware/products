// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage.EntityAttributeValue;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Extension methods for configuring data extension schema services on <see cref="IIdentityServerBuilder"/>.
/// </summary>
public static class DataExtensionSchemaBuilderExtensions
{
    /// <summary>
    ///     Registers a fixed set of data extension schemas using an in-memory store.
    ///     Schemas are immutable at runtime. <see cref="ISchemaAdmin"/> is NOT registered;
    ///     attempting to resolve it will fail.
    /// </summary>
    /// <param name="builder">The IdentityServer builder.</param>
    /// <param name="schemas">The schema definitions to make available.</param>
    /// <returns>The builder for chaining.</returns>
    public static IIdentityServerBuilder AddInMemoryDataExtensionSchemas(
        this IIdentityServerBuilder builder,
        IEnumerable<SchemaConfiguration> schemas)
    {
        var store = new InMemorySchemaStore(schemas);
        builder.Services.RemoveAll<ISchemaStore>();
        builder.Services.RemoveAll<ISchemaAdmin>();
        builder.Services.AddSingleton<ISchemaStore>(store);
        return builder;
    }

    /// <summary>
    ///     Registers data extension schema services backed by the database storage layer.
    ///     Both <see cref="ISchemaStore"/> (read) and <see cref="ISchemaAdmin"/> (write) are registered,
    ///     backed by the configured storage provider.
    /// </summary>
    /// <param name="builder">The IdentityServer builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IIdentityServerBuilder AddStorageDataExtensionSchemas(
        this IIdentityServerBuilder builder)
    {
        StorageSchemaAdmin.RegisterServices(builder.Services);
        return builder;
    }
}
