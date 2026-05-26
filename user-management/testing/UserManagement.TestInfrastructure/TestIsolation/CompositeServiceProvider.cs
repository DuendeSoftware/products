// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement.TestIsolation;

/// <summary>
/// Two-level DI container: resolves from the per-test provider first,
/// falls back to the global (shared) provider.
/// </summary>
internal sealed class CompositeServiceProvider :
    IServiceProvider,
    IServiceScopeFactory,
    IKeyedServiceProvider
{
    private readonly IServiceProvider _test;
    private readonly IServiceProvider _global;

    public CompositeServiceProvider(IServiceProvider test, IServiceProvider global)
    {
        _test = test;
        _global = global;
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IServiceScopeFactory))
        {
            return this;
        }

        var o = _test.GetService(serviceType);
        if (o != null)
        {
            return o;
        }

        return _global.GetService(serviceType);
    }

    // IServiceScopeFactory
    public IServiceScope CreateScope()
    {
        var testScope = _test.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var globalScope = _global.GetRequiredService<IServiceScopeFactory>().CreateScope();
        return new CompositeScope(testScope, globalScope);
    }

    // IKeyedServiceProvider
    public object? GetKeyedService(Type serviceType, object? serviceKey)
    {
        var testKeyed = _test as IKeyedServiceProvider;
        var result = testKeyed?.GetKeyedService(serviceType, serviceKey);
        if (result != null)
        {
            return result;
        }

        var globalKeyed = _global as IKeyedServiceProvider;
        return globalKeyed?.GetKeyedService(serviceType, serviceKey);
    }

    public object GetRequiredKeyedService(Type serviceType, object? serviceKey) =>
        GetKeyedService(serviceType, serviceKey)
            ?? throw new InvalidOperationException(
                $"No keyed service of type '{serviceType}' with key '{serviceKey}' registered.");

    private sealed class CompositeScope : IServiceScope
    {
        private readonly IServiceScope _testScope;
        private readonly IServiceScope _globalScope;

        public CompositeScope(IServiceScope testScope, IServiceScope globalScope)
        {
            _testScope = testScope;
            _globalScope = globalScope;
            ServiceProvider = new CompositeServiceProvider(
                testScope.ServiceProvider,
                globalScope.ServiceProvider);
        }

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
            _testScope.Dispose();
            _globalScope.Dispose();
        }
    }
}
