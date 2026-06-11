// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;

/// <summary>
/// Two-level DI container: resolves from the per-test provider first,
/// falls back to the global (shared) provider.
/// </summary>
internal sealed class CompositeServiceProvider(IServiceProvider test, IServiceProvider global) :
    IServiceProvider,
    IServiceScopeFactory,
    IKeyedServiceProvider
{
    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceScopeFactory))
        {
            return this;
        }

        var o = test.GetService(serviceType);
        if (o != null)
        {
            return o;
        }

        return global.GetService(serviceType);
    }

    public IServiceScope CreateScope()
    {
        var testScope = test.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var globalScope = global.GetRequiredService<IServiceScopeFactory>().CreateScope();
        return new CompositeScope(testScope, globalScope);
    }

    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        var testKeyed = test as IKeyedServiceProvider;
        var result = testKeyed?.GetKeyedService(serviceType, serviceKey);
        if (result != null)
        {
            return result;
        }

        var globalKeyed = global as IKeyedServiceProvider;
        return globalKeyed?.GetKeyedService(serviceType, serviceKey);
    }

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
        GetKeyedService(serviceType, serviceKey)
        ?? throw new InvalidOperationException(
            $"No keyed service of type '{serviceType}' with key '{serviceKey}' registered.");

    private sealed class CompositeScope(IServiceScope testScope, IServiceScope globalScope) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new CompositeServiceProvider(
            testScope.ServiceProvider,
            globalScope.ServiceProvider);

        public void Dispose()
        {
            testScope.Dispose();
            globalScope.Dispose();
        }
    }
}
