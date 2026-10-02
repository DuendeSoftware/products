// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement;

/// <summary>
/// Builder interface for configuring Duende User Management services.
/// </summary>
public interface IUserManagementBuilder
{
    internal IServiceCollection Services { get; }

    internal class Builder(IServiceCollection services) : IUserManagementBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}
