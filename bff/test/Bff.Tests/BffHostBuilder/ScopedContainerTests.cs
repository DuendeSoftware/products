// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Builder;

namespace Duende.Bff.Tests.BffHostBuilder;

public class ScopedContainerTests
{

    [Fact]
    public void Can_get_scoped_service_from_root_container()
    {

        var hostCollection = new ServiceCollection();
        hostCollection.AddScoped<DisposableObject>();
        var hostServiceProvider = hostCollection.BuildServiceProvider();

        var disposableFromRoot = hostServiceProvider.GetRequiredService<DisposableObject>();

        var scopedRootContainer = new RootContainer(hostServiceProvider);

        var appCollection = new ServiceCollection();
        appCollection.AddSingleton(scopedRootContainer);
        appCollection.AddDelegatedToRootContainer<DisposableObject>();

        var appServices = appCollection.BuildServiceProvider();

        // Make sure we can do this multiple times
        for (var i = 0; i < 10; i++)
        {
            // Create a scope (just like a HTTP request would)
            var scope = appServices.CreateScope();
            var disposableFromChild = scope.ServiceProvider.GetRequiredService<DisposableObject>();

            // The scope is live, so the disposable should not be disposed yet
            disposableFromChild.Disposed.ShouldBeFalse();

            // When the scope is disposed, the disposable should be disposed
            scope.Dispose();
            disposableFromChild.Disposed.ShouldBeTrue();

        }
        disposableFromRoot.Disposed.ShouldBeFalse();
    }

    [Fact]
    public void When_getting_scoped_from_root_always_gets_same_instance()
    {

        var hostCollection = new ServiceCollection();
        hostCollection.AddScoped<DisposableObject>();
        var hostServiceProvider = hostCollection.BuildServiceProvider();

        var disposableFromRoot = hostServiceProvider.GetRequiredService<DisposableObject>();

        var scopedRootContainer = new RootContainer(hostServiceProvider);

        var appCollection = new ServiceCollection();
        appCollection.AddSingleton(scopedRootContainer);
        appCollection.AddDelegatedToRootContainer<DisposableObject>();

        var appServices = appCollection.BuildServiceProvider();

        var scope = appServices.CreateScope();
        var disposable1 = scope.ServiceProvider.GetRequiredService<DisposableObject>();
        var disposable2 = scope.ServiceProvider.GetRequiredService<DisposableObject>();

        disposable2.ShouldBe(disposable1);
        scope.Dispose();

    }

    [Fact]
    public void Can_also_use_scoped_as_dependency_of_transient()
    {

        var hostCollection = new ServiceCollection();
        hostCollection.AddTransient<ObjectUsingDisposable>();
        hostCollection.AddScoped<DisposableObject>();
        var hostServiceProvider = hostCollection.BuildServiceProvider();

        var disposableFromRoot = hostServiceProvider.GetRequiredService<DisposableObject>();

        var scopedRootContainer = new RootContainer(hostServiceProvider);

        var appCollection = new ServiceCollection();
        appCollection.AddSingleton(scopedRootContainer);
        appCollection.AddDelegatedToRootContainer<ObjectUsingDisposable>();
        appCollection.AddDelegatedToRootContainer<DisposableObject>();

        var appServices = appCollection.BuildServiceProvider();

        // Make sure we can do this multiple times
        for (var i = 0; i < 10; i++)
        {
            // Create a scope (just like a HTTP request would)
            var scope = appServices.CreateScope();
            var disposableFromChild = scope.ServiceProvider.GetRequiredService<ObjectUsingDisposable>();

            // The scope is live, so the disposable should not be disposed yet
            disposableFromChild.Disposed.ShouldBeFalse();

            // When the scope is disposed, the disposable should be disposed
            scope.Dispose();
            disposableFromChild.Disposed.ShouldBeTrue();

        }
        disposableFromRoot.Disposed.ShouldBeFalse();
    }
    [Fact]
    public void Scoped_dependency_of_transient_is_always_same()
    {

        var hostCollection = new ServiceCollection();
        hostCollection.AddTransient<ObjectUsingDisposable>();
        hostCollection.AddScoped<DisposableObject>();
        var hostServiceProvider = hostCollection.BuildServiceProvider();

        var scopedRootContainer = new RootContainer(hostServiceProvider);

        var appCollection = new ServiceCollection();
        appCollection.AddSingleton(scopedRootContainer);
        appCollection.AddDelegatedToRootContainer<ObjectUsingDisposable>();
        appCollection.AddDelegatedToRootContainer<DisposableObject>();

        var appServices = appCollection.BuildServiceProvider();

        var scope = appServices.CreateScope();
        var ref1 = scope.ServiceProvider.GetRequiredService<ObjectUsingDisposable>();
        var ref2 = scope.ServiceProvider.GetRequiredService<ObjectUsingDisposable>();

        ref2.ShouldNotBe(ref1, "The transient object should be different");

        ref2.Obj.ShouldBe(ref1.Obj, "The scoped dependency should be the same");

        appServices.GetRequiredService<ObjectUsingDisposable>().Obj.ShouldNotBe(ref2.Obj,
            "But in a different scope you get a different scope");

        scope.Dispose();

    }

    public class ObjectUsingDisposable(DisposableObject obj)
    {
        public DisposableObject Obj { get; } = obj;

        public bool Disposed => Obj.Disposed;
    }

    public class DisposableObject : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose()
        {
            if (Disposed)
            {
                return;
            }

            Disposed = true;
        }
    }
}
