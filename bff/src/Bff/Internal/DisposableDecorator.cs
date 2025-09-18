// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Internal;

internal class DisposableDecorator<TService>(TService instance)
    : Decorator<TService>(instance), IDisposable
{
    public void Dispose() => (Instance as IDisposable)?.Dispose();
}
