// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using Duende.Storage;
using Duende.Storage.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement;

/// <summary>
/// Records the <see cref="DataCategoryName"/> resolved on each call to <see cref="IStorageInstanceRouter.Resolve"/>.
/// Registered as a singleton so tests can read what the decorated router resolved.
/// </summary>
public sealed class DataCategoryNameRecorder
{
    /// <summary>
    /// Registers itself as a spy around the IStorageInstanceRouter.
    /// </summary>
    /// <param name="sc">The service collection to register the spy in.</param>
    /// <returns>The recorder, which is also registered as a singleton.</returns>
    public static DataCategoryNameRecorder RegisterAsSpy(IServiceCollection sc)
    {
        ArgumentNullException.ThrowIfNull(sc);

        var recorder = new DataCategoryNameRecorder();
        _ = sc.AddSingleton(recorder);
        _ = Decorate<IStorageInstanceRouter>(sc, (inner, sp) =>
            new InstanceResolver(inner, recorder));

        return recorder;
    }

    /// <summary>
    /// Replaces the most recent registration of <typeparamref name="TService"/> with a decorator
    /// produced by <paramref name="decorate"/>. The decorator receives the original (inner) service
    /// instance and the <see cref="IServiceProvider"/>. The original registration's lifetime is preserved.
    /// </summary>
    private static IServiceCollection Decorate<TService>(IServiceCollection services, Func<TService, IServiceProvider, TService> decorate)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(decorate);

        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService))
                         ?? throw new InvalidOperationException(
                             $"No registration found for {typeof(TService).Name} to decorate.");
        _ = services.Remove(descriptor);

        services.Add(new ServiceDescriptor(
            typeof(TService),
            sp =>
            {
                var inner = (TService)(descriptor.ImplementationInstance
                                       ?? descriptor.ImplementationFactory?.Invoke(sp)
                                       ?? ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!));
                return decorate(inner, sp);
            },
            descriptor.Lifetime));

        return services;
    }

    // Concurrent: the router is a singleton and tests may resolve categories from several threads.
    private readonly ConcurrentDictionary<DataCategoryName, byte> _requestedCategories = new();

    public IReadOnlyCollection<DataCategoryName> RequestedCategories => [.. _requestedCategories.Keys];

    internal void Record(DataCategoryName category) => _requestedCategories.TryAdd(category, 0);

    public void UsedCategoriesShouldBe(params DataCategoryName[] categories) => RequestedCategories.ShouldBe(categories, ignoreOrder: true);

    public static void UsedCategoriesShouldBe(IServiceProvider services, params DataCategoryName[] categories)
    {
        var recorder = services.GetRequiredService<DataCategoryNameRecorder>();
        recorder.UsedCategoriesShouldBe(categories);

    }

    /// <summary>
    /// An <see cref="IStorageInstanceRouter"/> decorator that records the <see cref="DataCategoryName"/>
    /// resolved on each call before delegating to the wrapped router.
    /// </summary>
    public sealed class InstanceResolver(IStorageInstanceRouter inner, DataCategoryNameRecorder recorder) : IStorageInstanceRouter
    {
        public void AddMapping(DataCategoryName dataCategory, StorageInstanceId storageInstanceId) => inner.AddMapping(dataCategory, storageInstanceId);

        public StorageInstanceId Resolve(DataCategoryName dataCategory)
        {
            recorder.Record(dataCategory);

            return inner.Resolve(dataCategory);
        }

        public void RegisterInstance(StorageInstanceId storageInstanceId) => inner.RegisterInstance(storageInstanceId);

        public IReadOnlyCollection<StorageInstanceId> GetAll() => inner.GetAll();
    }
}


