// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces.DataProtection;

public sealed class SpacesDataProtectionRegistrationTests : IDisposable
{
    private const string AppName = "registration-tests";

    private static ServiceCollection NewServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        TestSpacesLicense.RegisterEntitled(services);
        return services;
    }

    // Builds the container and runs the app pipeline setup, so the startup check in UseSpaceResolution() runs too.
    private static ServiceProvider BuildApp(ServiceCollection services)
    {
        var sp = services.BuildServiceProvider();
        new ApplicationBuilder(sp).UseSpaceResolution();
        return sp;
    }

    private readonly string _keyDir = Path.Combine(Path.GetTempPath(), "spaces-dp-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_keyDir))
        {
            Directory.Delete(_keyDir, recursive: true);
        }
    }

    private IDataProtectionProvider Standalone() =>
        DataProtectionProvider.Create(new DirectoryInfo(_keyDir), b => b.SetApplicationName(AppName));

    [Fact]
    public void flag_off_resolves_the_apps_own_instance()
    {
        var own = new EphemeralDataProtectionProvider();
        var services = NewServices();
        services.AddSingleton<IDataProtectionProvider>(own);
        services.Configure<SpacesOptions>(o => o.PartitionDataProtection = false);
        services.AddSpacesInternal();
        using var sp = BuildApp(services);

        sp.GetRequiredService<IDataProtectionProvider>().ShouldBeSameAs(own);
    }

    [Fact]
    public void partitioning_is_on_by_default_and_wraps_the_apps_own_instance()
    {
        var own = new EphemeralDataProtectionProvider();
        var services = NewServices();
        services.AddSingleton<IDataProtectionProvider>(own);
        services.AddSpacesInternal();
        using var sp = BuildApp(services);

        var provider = sp.GetRequiredService<IDataProtectionProvider>();
        provider.ShouldNotBeSameAs(own);
        provider.GetUnpartitioned().ShouldBeSameAs(own);

        var accessor = sp.GetRequiredService<ISpaceContextAccessor>();
        var spaceId = SpaceId.New();
        byte[] protectedData;
        using (accessor.SetSpace(spaceId))
        {
            protectedData = provider.CreateProtector("p").Protect([1, 2, 3]);
        }

        own.CreateProtector("space").CreateProtector(spaceId.Value.ToString("D")).CreateProtector("p")
            .Unprotect(protectedData).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void flag_off_allows_cross_space_reads()
    {
        var services = NewServices();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.Configure<SpacesOptions>(o => o.PartitionDataProtection = false);
        services.AddSpacesInternal();
        using var sp = BuildApp(services);

        var provider = sp.GetRequiredService<IDataProtectionProvider>();
        var accessor = sp.GetRequiredService<ISpaceContextAccessor>();

        var spaceA = SpaceId.New();
        var spaceB = SpaceId.New();
        byte[] protectedData;
        using (accessor.SetSpace(spaceA))
        {
            protectedData = provider.CreateProtector("p").Protect([1, 2, 3]);
        }

        using (accessor.SetSpace(spaceB))
        {
            provider.CreateProtector("p").Unprotect(protectedData).ShouldBe([1, 2, 3]);
        }
    }

    [Fact]
    public void flag_configured_after_add_spaces_is_honoured()
    {
        var own = new EphemeralDataProtectionProvider();
        var services = NewServices();
        services.AddSingleton<IDataProtectionProvider>(own);
        services.AddSpacesInternal();
        services.Configure<SpacesOptions>(o => o.PartitionDataProtection = false);
        using var sp = BuildApp(services);

        sp.GetRequiredService<IDataProtectionProvider>().ShouldBeSameAs(own);
    }

    [Theory]
    [InlineData("spaces, data_protection, authentication")]
    [InlineData("spaces, authentication, data_protection")]
    [InlineData("data_protection, spaces, authentication")]
    [InlineData("data_protection, authentication, spaces")]
    [InlineData("authentication, spaces, data_protection")]
    [InlineData("authentication, data_protection, spaces")]
    public void registration_order_does_not_matter(string order)
    {
        var steps = new Dictionary<string, Action<IServiceCollection>>
        {
            ["spaces"] = s => s.AddSpacesInternal(),
            ["data_protection"] = s => s.AddDataProtection().SetApplicationName(AppName).PersistKeysToFileSystem(new DirectoryInfo(_keyDir)),
            ["authentication"] = s => s.AddAuthentication().AddCookie(),
        };
        var services = NewServices();
        foreach (var step in order.Split(", "))
        {
            steps[step](services);
        }

        using var sp = BuildApp(services);

        var provider = sp.GetRequiredService<IDataProtectionProvider>();
        sp.GetServices<IDataProtectionProvider>().ShouldHaveSingleItem().ShouldBeSameAs(provider);

        var protectedData = provider.GetUnpartitioned().CreateProtector("x").Protect([9]);
        Standalone().CreateProtector("x").Unprotect(protectedData).ShouldBe([9]);
        provider.GetUnpartitioned().ShouldNotBeSameAs(provider);
    }

    [Fact]
    public void multiple_non_keyed_registrations_wrap_only_the_last_and_keep_the_others()
    {
        var a = new EphemeralDataProtectionProvider();
        var b = new EphemeralDataProtectionProvider();
        var services = NewServices();
        services.AddSingleton<IDataProtectionProvider>(a);
        services.AddSingleton<IDataProtectionProvider>(b);
        services.AddSpacesInternal();

        // UseSpaceResolution() rejects this configuration (see SpacesDataProtectionValidationTests).
        // This covers a background-only host, which never calls it.
        using var sp = services.BuildServiceProvider();

        var provider = sp.GetRequiredService<IDataProtectionProvider>();
        provider.GetUnpartitioned().ShouldBeSameAs(b);

        var all = sp.GetServices<IDataProtectionProvider>().ToList();
        all.Count.ShouldBe(2);
        all[0].ShouldBeSameAs(a);
        all[1].ShouldBeSameAs(provider);
    }

    public enum OriginalRegistration
    {
        TransientType,
        SingletonFactory,
    }

    [Theory]
    [InlineData(OriginalRegistration.TransientType, true)]
    [InlineData(OriginalRegistration.TransientType, false)]
    [InlineData(OriginalRegistration.SingletonFactory, true)]
    [InlineData(OriginalRegistration.SingletonFactory, false)]
    public void original_provider_is_constructed_once(OriginalRegistration registration, bool partitionDataProtection)
    {
        var counter = new ConstructionCounter();
        var services = NewServices();
        services.AddSingleton(counter);
        if (registration == OriginalRegistration.TransientType)
        {
            services.AddTransient<IDataProtectionProvider, CountingProvider>();
        }
        else
        {
            services.AddSingleton<IDataProtectionProvider>(sp => new CountingProvider(sp.GetRequiredService<ConstructionCounter>()));
        }

        services.AddSpacesInternal();
        services.Configure<SpacesOptions>(o => o.PartitionDataProtection = partitionDataProtection);
        using var sp = BuildApp(services);

        var first = sp.GetRequiredService<IDataProtectionProvider>();
        var second = sp.GetRequiredService<IDataProtectionProvider>();
        var all = sp.GetServices<IDataProtectionProvider>().ToList();
        _ = first.GetUnpartitioned();

        second.ShouldBeSameAs(first);
        all.ShouldHaveSingleItem().ShouldBeSameAs(first);
        counter.Count.ShouldBe(1);
    }

    [Fact]
    public void scoped_original_provider_throws_at_registration()
    {
        var services = NewServices();
        services.AddScoped<IDataProtectionProvider>(_ => new EphemeralDataProtectionProvider());

        Should.Throw<InvalidOperationException>(() => services.AddSpacesInternal())
            .Message.ShouldBe(
                "IDataProtectionProvider is registered as scoped, which Spaces data protection does not support. " +
                "Register it as a singleton before AddSpaces().");
    }

    [Fact]
    public void same_descriptor_instance_registered_twice_partitions_the_effective_registration()
    {
        var own = new EphemeralDataProtectionProvider();
        var descriptor = ServiceDescriptor.Singleton<IDataProtectionProvider>(own);
        var services = NewServices();
        ((IServiceCollection)services).Add(descriptor);
        ((IServiceCollection)services).Add(descriptor);
        services.AddSpacesInternal();

        // UseSpaceResolution() rejects this configuration (see SpacesDataProtectionValidationTests).
        // This covers a background-only host, which never calls it.
        using var sp = services.BuildServiceProvider();

        var resolved = sp.GetRequiredService<IDataProtectionProvider>();
        resolved.ShouldNotBeSameAs(own);
        resolved.GetUnpartitioned().ShouldBeSameAs(own);
    }

    [Fact]
    public void keyed_registration_is_untouched()
    {
        var keyed = new EphemeralDataProtectionProvider();
        var own = new EphemeralDataProtectionProvider();
        var services = NewServices();
        services.AddKeyedSingleton<IDataProtectionProvider>("k", keyed);
        services.AddSingleton<IDataProtectionProvider>(own);
        services.AddSpacesInternal();
        using var sp = BuildApp(services);

        sp.GetRequiredKeyedService<IDataProtectionProvider>("k").ShouldBeSameAs(keyed);
        var resolved = sp.GetRequiredService<IDataProtectionProvider>();
        resolved.ShouldNotBeSameAs(own);
        resolved.GetUnpartitioned().ShouldBeSameAs(own);
    }

    [Fact]
    public void flag_on_disposes_an_original_registered_by_type()
    {
        var counter = new DisposalCounter();
        var services = NewServices();
        services.AddSingleton(counter);
        services.AddSingleton<IDataProtectionProvider, DisposableProvider>();
        services.AddSpacesInternal();
        var sp = BuildApp(services);
        _ = sp.GetRequiredService<IDataProtectionProvider>();

        sp.Dispose();

        counter.Disposed.ShouldBe(1);
    }

    [Fact]
    public async Task flag_on_prefers_async_disposal_of_an_original_registered_by_factory()
    {
        var counter = new DisposalCounter();
        var services = NewServices();
        services.AddSingleton<IDataProtectionProvider>(_ => new AsyncDisposableProvider(counter));
        services.AddSpacesInternal();
        var sp = BuildApp(services);
        _ = sp.GetRequiredService<IDataProtectionProvider>();

        await sp.DisposeAsync();

        counter.AsyncDisposed.ShouldBe(1);
        counter.Disposed.ShouldBe(0);
    }

    [Fact]
    public void flag_on_does_not_dispose_an_original_registered_as_an_instance()
    {
        var counter = new DisposalCounter();
        var services = NewServices();
        services.AddSingleton<IDataProtectionProvider>(new DisposableProvider(counter));
        services.AddSpacesInternal();
        var sp = BuildApp(services);
        _ = sp.GetRequiredService<IDataProtectionProvider>();

        sp.Dispose();

        counter.Disposed.ShouldBe(0);
    }

    [Fact]
    public void flag_off_disposes_an_original_registered_by_type()
    {
        // With partitioning off the original is both the keyed registration and the result of the partitioned
        // provider's factory, so the container may dispose it more than once. Dispose must be idempotent.
        var counter = new DisposalCounter();
        var services = NewServices();
        services.AddSingleton(counter);
        services.AddSingleton<IDataProtectionProvider, DisposableProvider>();
        services.Configure<SpacesOptions>(o => o.PartitionDataProtection = false);
        services.AddSpacesInternal();
        var sp = BuildApp(services);
        _ = sp.GetRequiredService<IDataProtectionProvider>();

        sp.Dispose();

        counter.Disposed.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task flag_on_async_disposes_an_async_only_original_registered_by_type()
    {
        var counter = new DisposalCounter();
        var sp = BuildWith<AsyncOnlyProvider>(counter, partition: true);
        _ = sp.GetRequiredService<IDataProtectionProvider>();

        await sp.DisposeAsync();

        counter.AsyncDisposed.ShouldBe(1);
    }

    [Fact]
    public void flag_on_sync_dispose_of_an_async_only_original_throws_like_the_container_does_without_the_wrapper()
    {
        var onCounter = new DisposalCounter();
        var on = BuildWith<AsyncOnlyProvider>(onCounter, partition: true);
        _ = on.GetRequiredService<IDataProtectionProvider>();
        var offCounter = new DisposalCounter();
        var off = BuildWith<AsyncOnlyProvider>(offCounter, partition: false);
        _ = off.GetRequiredService<IDataProtectionProvider>();

        _ = Should.Throw<InvalidOperationException>(() => off.Dispose());
        var thrown = Should.Throw<InvalidOperationException>(() => on.Dispose());

        thrown.Message.ShouldContain("only implements IAsyncDisposable");
    }

    [Fact]
    public async Task flag_on_async_disposes_a_sync_only_original_once()
    {
        var counter = new DisposalCounter();
        var sp = BuildWith<DisposableProvider>(counter, partition: true);
        _ = sp.GetRequiredService<IDataProtectionProvider>();

        await sp.DisposeAsync();

        counter.Disposed.ShouldBe(1);
    }

    [Fact]
    public async Task flag_off_async_disposes_an_async_only_original_registered_by_type()
    {
        // See flag_off_disposes_an_original_registered_by_type: more than one call is allowed.
        var counter = new DisposalCounter();
        var sp = BuildWith<AsyncOnlyProvider>(counter, partition: false);
        _ = sp.GetRequiredService<IDataProtectionProvider>();

        await sp.DisposeAsync();

        counter.AsyncDisposed.ShouldBeGreaterThanOrEqualTo(1);
    }

    private static ServiceProvider BuildWith<T>(DisposalCounter counter, bool partition)
        where T : class, IDataProtectionProvider
    {
        var services = NewServices();
        services.AddSingleton(counter);
        services.AddSingleton<IDataProtectionProvider, T>();
        services.Configure<SpacesOptions>(o => o.PartitionDataProtection = partition);
        services.AddSpacesInternal();
        return BuildApp(services);
    }

    private sealed class AsyncOnlyProvider(DisposalCounter counter) : IDataProtectionProvider, IAsyncDisposable
    {
        private readonly EphemeralDataProtectionProvider _inner = new();

        public IDataProtector CreateProtector(string purpose) => _inner.CreateProtector(purpose);

        public ValueTask DisposeAsync()
        {
            counter.AsyncDisposed++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DisposalCounter
    {
        public int Disposed { get; set; }

        public int AsyncDisposed { get; set; }
    }

    private sealed class DisposableProvider(DisposalCounter counter) : IDataProtectionProvider, IDisposable
    {
        private readonly EphemeralDataProtectionProvider _inner = new();

        public IDataProtector CreateProtector(string purpose) => _inner.CreateProtector(purpose);

        public void Dispose() => counter.Disposed++;
    }

    private sealed class AsyncDisposableProvider(DisposalCounter counter) : IDataProtectionProvider, IDisposable, IAsyncDisposable
    {
        private readonly EphemeralDataProtectionProvider _inner = new();

        public IDataProtector CreateProtector(string purpose) => _inner.CreateProtector(purpose);

        public void Dispose() => counter.Disposed++;

        public ValueTask DisposeAsync()
        {
            counter.AsyncDisposed++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ConstructionCounter
    {
        public int Count { get; set; }
    }

    private sealed class CountingProvider : IDataProtectionProvider
    {
        private readonly EphemeralDataProtectionProvider _inner = new();

        public CountingProvider(ConstructionCounter counter) => counter.Count++;

        public IDataProtector CreateProtector(string purpose) => _inner.CreateProtector(purpose);
    }
}
