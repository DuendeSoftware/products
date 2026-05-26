// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement.Internal;

public static class UserManagementServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUserManagementInternal(Action<IUserManagementBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);
            configure(new IUserManagementBuilder.Builder(services));

            return services;
        }
    }
}
