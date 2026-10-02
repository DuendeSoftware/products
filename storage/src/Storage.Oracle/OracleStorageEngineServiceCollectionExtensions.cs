// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Oracle.Internal;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.Storage.Oracle;

public static class OracleStorageEngineServiceCollectionExtensions
{
    extension(IStorageBuilder builder)
    {
        /// <summary>
        /// Adds an Oracle store, resolving the app-owned <see cref="CreateOracleConnection"/> via
        /// the supplied <paramref name="resolver"/> each time a store instance is created.
        /// </summary>
        /// <param name="resolver">
        /// A callback that returns the app-owned <see cref="CreateOracleConnection"/> delegate to use.
        /// Storage never caches, registers, owns, or disposes the resolved delegate; the
        /// application remains fully responsible for its lifetime. The resolver is invoked
        /// against the current scope's <see cref="IServiceProvider"/> every time a store is
        /// created, so it should be cheap (e.g. a simple keyed or unkeyed service lookup).
        /// </param>
        /// <param name="configure">An optional callback used to configure the store options.</param>
        /// <remarks>
        /// Use this overload for named storage instances; it is the only way to supply the
        /// <see cref="CreateOracleConnection"/> for an instance other than the default.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a database provider has already been selected for the builder's storage instance.
        /// </exception>
        public IStorageBuilder AddOracle(Func<IServiceProvider, CreateOracleConnection> resolver, Action<OracleStorageEngineOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(resolver);
            builder.MarkProviderSelected("Oracle");

            var services = builder.Services;
            _ = services.AddStore<OracleStorageEngine>(builder.StorageInstanceId);
            _ = services.AddKeyedTransient<OracleStorageEngine>(builder.StorageInstanceId, (sp, _) =>
            {
                var createConnection = ResolveCreateConnection(resolver, sp, builder.StorageInstanceId);
                var outboxSubscriptions = sp.GetRequiredKeyedService<OutboxSubscriptions>(builder.StorageInstanceId);
                return BuildStore(sp, createConnection, outboxSubscriptions, configure ?? (_ => { }));
            });
            return builder;
        }

        /// <summary>
        /// Adds an Oracle store for the default storage instance, using the
        /// <see cref="CreateOracleConnection"/> registered in the service collection.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the builder is for a named storage instance (use the
        /// <c>AddOracle(Func&lt;IServiceProvider, CreateOracleConnection&gt; resolver, Action&lt;OracleStorageEngineOptions&gt;? configure = null)</c>
        /// resolver overload instead), or when a database provider has already been selected for the
        /// default storage instance.
        /// </exception>
        public IStorageBuilder AddOracle(Action<OracleStorageEngineOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            if (builder.StorageInstanceId != StorageInstanceId.Default)
            {
                throw new InvalidOperationException(
                    "AddOracle(Action<OracleStorageEngineOptions>) can only be used for the default storage instance, " +
                    $"not the named instance '{builder.StorageInstanceId}'. Use the resolver overload instead: " +
                    "AddOracle(Func<IServiceProvider, CreateOracleConnection> resolver, Action<OracleStorageEngineOptions>? configure = null), " +
                    "supplying a resolver that returns the app-owned CreateOracleConnection for this instance.");
            }

            return builder.AddOracle(sp => sp.GetRequiredService<CreateOracleConnection>(), configure);
        }
    }

    private static CreateOracleConnection ResolveCreateConnection(
        Func<IServiceProvider, CreateOracleConnection> resolver,
        IServiceProvider sp,
        StorageInstanceId storageInstanceId)
    {
        CreateOracleConnection? createConnection;
        try
        {
            createConnection = resolver(sp);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"The Oracle CreateOracleConnection resolver for the storage instance '{storageInstanceId}' threw an exception. " +
                "Ensure the resolver returns a valid, app-owned CreateOracleConnection delegate.", ex);
        }

        if (createConnection is null)
        {
            throw new InvalidOperationException(
                $"The Oracle CreateOracleConnection resolver for the storage instance '{storageInstanceId}' returned null. " +
                "Ensure the resolver returns a valid, app-owned CreateOracleConnection delegate.");
        }

        return createConnection;
    }

    private static OracleStorageEngine BuildStore(
        IServiceProvider sp,
        CreateOracleConnection createConnection,
        OutboxSubscriptions outboxSubscriptions,
        Action<OracleStorageEngineOptions> configure)
    {
        var options = new OracleStorageEngineOptions();
        configure(options);
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        return new OracleStorageEngine(
            createConnection,
            options,
            sp.GetRequiredService<DataStorageTypeRegistry>(),
            sp.GetRequiredService<TimeProvider>(),
            outboxSubscriptions,
            sp.GetRequiredService<ILogger<OracleStorageEngine>>());
    }
}
