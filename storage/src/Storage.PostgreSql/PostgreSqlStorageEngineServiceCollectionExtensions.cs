// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.PostgreSql.Internal;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Duende.Storage.PostgreSql;

public static class PostgreSqlStorageEngineServiceCollectionExtensions
{
    extension(IStorageBuilder builder)
    {
        /// <summary>
        /// Adds a PostgreSQL store for the builder's storage instance, resolving the app-owned
        /// <see cref="NpgsqlDataSource"/> via the supplied <paramref name="resolver"/> each time a
        /// store is created. Use this overload for named storage instances.
        /// </summary>
        /// <param name="resolver">A callback that returns the app-owned <see cref="NpgsqlDataSource"/> to use.</param>
        /// <param name="configure">An optional callback used to configure the store options.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a database provider has already been selected for the builder's storage instance.
        /// </exception>
        public IStorageBuilder AddPostgreSql(Func<IServiceProvider, NpgsqlDataSource> resolver, Action<PostgreSqlStorageEngineOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(resolver);

            builder.MarkProviderSelected("PostgreSQL");

            var services = builder.Services;
            _ = services.AddStore<PostgreSqlStorageEngine>(builder.StorageInstanceId);
            _ = services.AddKeyedTransient<PostgreSqlStorageEngine>(builder.StorageInstanceId, (sp, _) =>
            {
                var dataSource = ResolveDataSource(resolver, sp, builder.StorageInstanceId);
                var outboxSubscriptions = sp.GetRequiredKeyedService<OutboxSubscriptions>(builder.StorageInstanceId);
                return BuildStore(sp, dataSource, outboxSubscriptions, configure ?? (_ => { }));
            });
            return builder;
        }

        /// <summary>
        /// Adds a PostgreSQL store for the default storage instance, using the
        /// <see cref="NpgsqlDataSource"/> registered in the service collection.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the builder is for a named storage instance, or when a database provider has
        /// already been selected for the default storage instance.
        /// </exception>
        public IStorageBuilder AddPostgreSql() => builder.AddPostgreSql(_ => { });

        /// <summary>
        /// Adds a PostgreSQL store for the default storage instance, using the
        /// <see cref="NpgsqlDataSource"/> registered in the service collection.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the builder is for a named storage instance (use the
        /// <c>AddPostgreSql(Func&lt;IServiceProvider, NpgsqlDataSource&gt; resolver, Action&lt;PostgreSqlStorageEngineOptions&gt;? configure = null)</c>
        /// resolver overload instead), or when a database provider has already been selected for the
        /// default storage instance.
        /// </exception>
        public IStorageBuilder AddPostgreSql(Action<PostgreSqlStorageEngineOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            if (builder.StorageInstanceId != StorageInstanceId.Default)
            {
                throw new InvalidOperationException(
                    "AddPostgreSql(Action<PostgreSqlStorageEngineOptions>) can only be used for the default storage instance, " +
                    $"not the named instance '{builder.StorageInstanceId}'. Use the resolver overload instead: " +
                    "AddPostgreSql(Func<IServiceProvider, NpgsqlDataSource> resolver, Action<PostgreSqlStorageEngineOptions>? configure = null), " +
                    "supplying a resolver that returns the app-owned NpgsqlDataSource for this instance.");
            }

            return builder.AddPostgreSql(sp => sp.GetRequiredService<NpgsqlDataSource>(), configure);
        }
    }

    private static NpgsqlDataSource ResolveDataSource(
        Func<IServiceProvider, NpgsqlDataSource> resolver,
        IServiceProvider sp,
        StorageInstanceId storageInstanceId)
    {
        NpgsqlDataSource? dataSource;
        try
        {
            dataSource = resolver(sp);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"The PostgreSQL NpgsqlDataSource resolver for the storage instance '{storageInstanceId}' threw an exception. " +
                "Ensure the resolver returns a valid, app-owned NpgsqlDataSource instance.", ex);
        }

        if (dataSource is null)
        {
            throw new InvalidOperationException(
                $"The PostgreSQL NpgsqlDataSource resolver for the storage instance '{storageInstanceId}' returned null. " +
                "Ensure the resolver returns a valid, app-owned NpgsqlDataSource instance.");
        }

        return dataSource;
    }

    private static PostgreSqlStorageEngine BuildStore(
        IServiceProvider sp,
        NpgsqlDataSource dataSource,
        OutboxSubscriptions outboxSubscriptions,
        Action<PostgreSqlStorageEngineOptions> configure)
    {
        var options = new PostgreSqlStorageEngineOptions();
        configure(options);
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        return new PostgreSqlStorageEngine(
            dataSource,
            options,
            sp.GetRequiredService<DataStorageTypeRegistry>(),
            sp.GetRequiredService<TimeProvider>(),
            outboxSubscriptions,
            sp.GetRequiredService<ILogger<PostgreSqlStorageEngine>>());
    }
}
