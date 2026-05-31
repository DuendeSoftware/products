// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement.Authentication;

/// <summary>
/// Builder interface for configuring user authentication services.
/// </summary>
public interface IUserAuthenticationBuilder
{
    /// <summary>
    /// Gets the service collection used to register authentication dependencies.
    /// </summary>
    IServiceCollection Services { get; }

    internal class FeatureBuilder(IServiceCollection services) : IUserAuthenticationBuilder
    {
        public IServiceCollection Services => services;
    }
}
