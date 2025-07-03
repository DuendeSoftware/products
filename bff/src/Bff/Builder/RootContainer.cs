// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Builder;

internal class RootContainer(IServiceProvider services)
{
    internal IServiceProvider Services { get; } = services;
    internal sealed class DelegatedScope(RootContainer root) : IDisposable
    {
        private readonly IServiceScope _scope = root.Services.CreateScope();

        public T Resolve<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

        public void Dispose() => _scope.Dispose();
    }
}
