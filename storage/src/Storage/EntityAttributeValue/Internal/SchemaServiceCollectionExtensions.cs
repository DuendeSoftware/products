// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue.Internal.Storage;
using Duende.Storage.Internal;
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
    public static void AddDynamicSchemaStorage(this IServiceCollection services) =>
        RegisterDynamicSchemaStorage(services, StorageInstanceId.Default);

    /// <summary>
    ///     Registers <see cref="StorageSchemaAdmin"/> as both <see cref="ISchemaStore"/> and
    ///     <see cref="ISchemaAdmin"/> in the service collection, including DSO registration, using
    ///     the supplied <paramref name="storageInstanceId"/> to select the underlying storage instance.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="storageInstanceId">The storage instance to use for dynamic schema storage.</param>
    public static void AddDynamicSchemaStorage(this IServiceCollection services, StorageInstanceId storageInstanceId) =>
        RegisterDynamicSchemaStorage(services, storageInstanceId);

    /// <summary>
    ///     Registers <paramref name="schema"/> as a product default, unless a schema with the same id is already
    ///     registered. A customer's schema for the same id wins in either registration order.
    /// </summary>
    /// <remarks>Only schemas registered as instances are detected.</remarks>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="schema">The default schema to register.</param>
    public static void TryAddSchema(this IServiceCollection services, SchemaConfiguration schema)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(schema);

        var alreadyRegistered = services.Any(d =>
            !d.IsKeyedService &&
            d.ServiceType == typeof(SchemaConfiguration) &&
            d.ImplementationInstance is SchemaConfiguration existing &&
            existing.SchemaId.Equals(schema.SchemaId));

        if (!alreadyRegistered)
        {
            _ = services.AddSingleton(schema);
        }
    }

    private static void RegisterDynamicSchemaStorage(IServiceCollection services, StorageInstanceId storageInstanceId)
    {
        var storageInstanceRouter = services.GetOrAddStorageInstanceRouter();
        storageInstanceRouter.AddMapping(DataCategoryName.DynamicSchemas, storageInstanceId);

        services.AddDsoRegistration<AttributeSchemaDso.V1>();
        _ = services.AddTransient<ISchemaStore, StorageSchemaAdmin>();
        _ = services.AddTransient<ISchemaAdmin, StorageSchemaAdmin>();
    }
}
