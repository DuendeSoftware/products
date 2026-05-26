// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement;

public interface IUserManagementBuilder : IStorageBuilder
{

    internal new IServiceCollection Services { get; }

#pragma warning disable CA1033 // Interface methods should be callable by child types
    IServiceCollection IStorageBuilder.Services => Services;
#pragma warning restore CA1033

    internal class Builder(IServiceCollection services) : IUserManagementBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}
