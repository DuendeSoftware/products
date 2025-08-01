// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.AspNetIdentity;

internal class Decorator<TService>
{
    public TService Instance { get; set; }

    public Decorator(TService instance) => Instance = instance;
}

#pragma warning disable CA1812 // This class is not instantiated directly, but rather used by the DI container
internal sealed class Decorator<TService, TImpl> : Decorator<TService>
    where TImpl : class, TService
#pragma warning restore CA1812
{
    public Decorator(TImpl instance) : base(instance)
    {
    }
}

internal sealed class DisposableDecorator<TService> : Decorator<TService>, IDisposable
{
    public DisposableDecorator(TService instance) : base(instance)
    {
    }

    public void Dispose() => (Instance as IDisposable)?.Dispose();
}
