// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Services;
using Duende.IdentityServer.UserManagement;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Scim;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer;

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

            builder.Services.TryAddScoped<IPasskeySignInHandler, IdentityServerPasskeySignInHandler>();
            _ = builder.Services.AddUserManagementInternal(configure);

            // Replace the default IdentityServer profile service with ours, but preserve
            // any custom registration the user may have added.
            var defaultDescriptor = builder.Services.FirstOrDefault(d =>
                d.ServiceType == typeof(IProfileService) &&
                d.ImplementationType == typeof(DefaultProfileService));
            if (defaultDescriptor != null)
            {
                _ = builder.Services.Remove(defaultDescriptor);
            }

            builder.Services.TryAddTransient<IProfileService, UserManagementProfileService>();

            // Register SCIM authority auto-resolution from IdentityServerOptions.IssuerUri
#pragma warning disable duende_experimental
            builder.Services.TryAddSingleton<IPostConfigureOptions<ScimOAuthOptions>, ScimAuthorityPostConfigureOptions>();
#pragma warning restore duende_experimental

            return builder;
        }
    }
}
