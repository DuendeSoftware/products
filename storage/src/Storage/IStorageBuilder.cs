// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage;

public interface IStorageBuilder
{
    public IServiceCollection Services { get; }
}
