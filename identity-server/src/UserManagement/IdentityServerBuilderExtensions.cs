// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.UserManagement;
using Duende.UserManagement;
using Duende.UserManagement.Internal;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods to add Duende UserManagement support to IdentityServer.
/// </summary>
public static class IdentityServerBuilderExtensions
{
    extension(IIdentityServerBuilder builder)
    {
        /// <summary>
        /// Configures IdentityServer to use Duende UserManagement for user profiles, authentication, and membership.
        /// </summary>
        /// <param name="configure">A delegate to configure the user management builder, including storage.</param>
        /// <returns>The same builder instance.</returns>
        public IIdentityServerBuilder AddUserManagement(
            Action<IUserManagementBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);

            builder.Services.AddUserManagementInternal(configure);
            builder.AddProfileService<UserManagementProfileService>();

            return builder;
        }
    }
}
