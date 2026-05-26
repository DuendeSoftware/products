// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement.Authentication;

public interface IUserAuthenticationBuilder
{
    IServiceCollection Services { get; }

    internal class FeatureBuilder(IServiceCollection services) : IUserAuthenticationBuilder
    {
        public IServiceCollection Services => services;
    }
}
