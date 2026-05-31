// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.Storage.Schema;

/// <summary>
/// A builder interface for configuring storage services.
/// </summary>
public interface IStorageBuilder
{
    /// <summary>
    /// Gets the service collection used to register storage services.
    /// </summary>
    public IServiceCollection Services { get; }
}
