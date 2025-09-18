// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Internal;

internal class Decorator<TService>(TService instance)
{
    public TService Instance { get; set; } = instance;
}

internal class Decorator<TService, TImpl>(TImpl instance) : Decorator<TService>(instance)
    where TImpl : class, TService;
