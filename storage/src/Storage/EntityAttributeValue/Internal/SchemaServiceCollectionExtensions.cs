// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.EntityAttributeValue.Internal;

/// <summary>
/// Extension methods for registering dynamic (database-backed) schema storage services.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public static class SchemaServiceCollectionExtensions
{
    /// <summary>
    ///     Registers <see cref="StorageSchemaAdmin"/> as both <see cref="ISchemaStore"/> and
    ///     <see cref="ISchemaAdmin"/> in the service collection, including DSO registration.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    public static void AddDynamicSchemaStorage(this IServiceCollection services)
    {
        services.AddDsoRegistration<AttributeSchemaDso.V1>();
        _ = services.AddTransient<ISchemaStore, StorageSchemaAdmin>();
        _ = services.AddTransient<ISchemaAdmin, StorageSchemaAdmin>();
    }
}
