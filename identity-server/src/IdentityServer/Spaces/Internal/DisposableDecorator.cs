// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.Spaces.Internal;

internal class DisposableDecorator<TService>(TService instance)
    : Decorator<TService>(instance), IDisposable
{
    public void Dispose() => (Instance as IDisposable)?.Dispose();
}
