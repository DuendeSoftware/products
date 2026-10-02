// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.MsSql.Internal;
using Duende.Storage.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.Storage.MsSql;

public static class MsSqlStorageEngineServiceCollectionExtensions
{
    extension(IStorageBuilder builder)
    {
        /// <summary>
        /// Adds a SQL Server store for the builder's storage instance, resolving the app-owned
        /// <see cref="CreateSqlConnection"/> via the supplied <paramref name="resolver"/> each time a
        /// store is created. Use this overload for named storage instances.
        /// </summary>
        /// <param name="resolver">A callback that returns the app-owned <see cref="CreateSqlConnection"/> delegate to use.</param>
        /// <param name="configure">An optional callback used to configure the store options.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a database provider has already been selected for the builder's storage instance.
        /// </exception>
        public IStorageBuilder AddMsSql(Func<IServiceProvider, CreateSqlConnection> resolver, Action<MsSqlStorageEngineOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(resolver);

            builder.MarkProviderSelected("SQL Server");

            var services = builder.Services;
            _ = services.AddStore<MsSqlStorageEngine>(builder.StorageInstanceId);
            _ = services.AddKeyedTransient<MsSqlStorageEngine>(builder.StorageInstanceId, (sp, _) =>
            {
                var createConnection = ResolveCreateConnection(resolver, sp, builder.StorageInstanceId);
                var outboxSubscriptions = sp.GetRequiredKeyedService<OutboxSubscriptions>(builder.StorageInstanceId);
                return BuildStore(sp, createConnection, outboxSubscriptions, configure ?? (_ => { }));
            });
            return builder;
        }

        /// <summary>
        /// Adds a SQL Server store for the default storage instance, using the
        /// <see cref="CreateSqlConnection"/> registered in the service collection.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the builder is for a named storage instance (use the
        /// <c>AddMsSql(Func&lt;IServiceProvider, CreateSqlConnection&gt; resolver, Action&lt;MsSqlStorageEngineOptions&gt;? configure = null)</c>
        /// resolver overload instead), or when a database provider has already been selected for the
        /// default storage instance.
        /// </exception>
        public IStorageBuilder AddMsSql(Action<MsSqlStorageEngineOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            if (builder.StorageInstanceId != StorageInstanceId.Default)
            {
                throw new InvalidOperationException(
                    "AddMsSql(Action<MsSqlStorageEngineOptions>) can only be used for the default storage instance, " +
                    $"not the named instance '{builder.StorageInstanceId}'. Use the resolver overload instead: " +
                    "AddMsSql(Func<IServiceProvider, CreateSqlConnection> resolver, Action<MsSqlStorageEngineOptions>? configure = null), " +
                    "supplying a resolver that returns the app-owned CreateSqlConnection for this instance.");
            }

            return builder.AddMsSql(sp => sp.GetRequiredService<CreateSqlConnection>(), configure);
        }
    }

    private static CreateSqlConnection ResolveCreateConnection(
        Func<IServiceProvider, CreateSqlConnection> resolver,
        IServiceProvider sp,
        StorageInstanceId storageInstanceId)
    {
        CreateSqlConnection? createConnection;
        try
        {
            createConnection = resolver(sp);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"The SQL Server CreateSqlConnection resolver for the storage instance '{storageInstanceId}' threw an exception. " +
                "Ensure the resolver returns a valid, app-owned CreateSqlConnection delegate.", ex);
        }

        if (createConnection is null)
        {
            throw new InvalidOperationException(
                $"The SQL Server CreateSqlConnection resolver for the storage instance '{storageInstanceId}' storage returned null. " +
                "Ensure the resolver returns a valid, app-owned CreateSqlConnection delegate.");
        }

        return createConnection;
    }

    private static MsSqlStorageEngine BuildStore(
        IServiceProvider sp,
        CreateSqlConnection createConnection,
        OutboxSubscriptions outboxSubscriptions,
        Action<MsSqlStorageEngineOptions> configure)
    {
        var options = new MsSqlStorageEngineOptions();
        configure(options);
        Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);
        return new MsSqlStorageEngine(
            createConnection,
            options,
            sp.GetRequiredService<DataStorageTypeRegistry>(),
            sp.GetRequiredService<TimeProvider>(),
            outboxSubscriptions,
            sp.GetRequiredService<ILogger<MsSqlStorageEngine>>());
    }
}

