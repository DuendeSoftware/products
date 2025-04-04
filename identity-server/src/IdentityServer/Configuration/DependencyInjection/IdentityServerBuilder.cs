// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// IdentityServer helper class for DI configuration
/// </summary>
public class IdentityServerBuilder : IIdentityServerBuilder
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityServerBuilder"/> class.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <exception cref="System.ArgumentNullException">services</exception>
    public IdentityServerBuilder(IServiceCollection services) => Services = services ?? throw new ArgumentNullException(nameof(services));

    /// <summary>
    /// Gets the services.
    /// </summary>
    /// <value>
    /// The services.
    /// </value>
    public IServiceCollection Services { get; }
}
