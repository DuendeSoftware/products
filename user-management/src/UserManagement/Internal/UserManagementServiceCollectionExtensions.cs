// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Internal;
using Duende.UserManagement.Internal.Modules;
using Duende.UserManagement.Membership.Internal;
using Duende.UserManagement.Profiles.Internal;
using Duende.UserManagement.Scim.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement.Internal;

/// <summary>
/// Extension methods for registering user management services with an <see cref="IServiceCollection"/>.
/// </summary>
public static class UserManagementServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds user management services to the service collection using the provided configuration delegate.
        /// </summary>
        /// <param name="configure">A delegate to configure the user management builder.</param>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddUserManagementInternal(Action<IUserManagementBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);
            configure(new IUserManagementBuilder.Builder(services));

            services.RegisterModule<UserProfilesModule>();
            services.RegisterModule<UserAuthenticationModule>();
            services.RegisterModule<UserMembershipModule>();
            services.RegisterModule<ScimModule>();

            return services;
        }
    }
}
