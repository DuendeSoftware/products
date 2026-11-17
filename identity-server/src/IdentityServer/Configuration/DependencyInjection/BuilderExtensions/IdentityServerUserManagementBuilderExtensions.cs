// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Services;
using Duende.IdentityServer.UserManagement;
using Duende.Spaces;
using Duende.Storage;
using Duende.IdentityServer.DataExtensions;
using Duende.IdentityServer.DataExtensions.Internal;
using Duende.Storage.Internal;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Internal;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Internal.Licensing;
using Duende.UserManagement.Internal.Modules;
using Duende.UserManagement.Internal.Storage;
using Duende.UserManagement.Membership.Internal;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Profiles.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        /// <param name="storageInstanceId">
        /// The storage instance that stores User Management data. A database provider must be
        /// registered for this instance, for example with <c>AddStorage(storageInstanceId, ...)</c>.
        /// </param>
        /// <param name="configure">A delegate to configure the user management builder, including storage.</param>
        /// <returns>The same builder instance.</returns>
        public IIdentityServerBuilder AddUserManagement(StorageInstanceId storageInstanceId,
            Action<IUserManagementBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);

            builder.Services.TryAddScoped<IPasskeySignInHandler, IdentityServerPasskeySignInHandler>();
            builder.Services.TryAddSingleton<ISchemaStore, InMemorySchemaStore>();

            // Select the appropriate provider after the host has finished registering services.
            // The scoped provider can safely inject Space services and the scoped user repository.
            builder.Services.TryAddScoped<IUserCountProvider>(sp =>
            {
                var serviceAvailability = sp.GetRequiredService<IServiceProviderIsService>();
                var hasSpaceAdmin = serviceAvailability.IsService(typeof(ISpaceAdmin));
                var hasSpaceContextAccessor = serviceAvailability.IsService(typeof(ISpaceContextAccessor));

                if (hasSpaceAdmin != hasSpaceContextAccessor)
                {
                    throw new InvalidOperationException(
                        "Spaces registration is incomplete. Both ISpaceAdmin and ISpaceContextAccessor are required to count licensed users across spaces.");
                }

                return hasSpaceAdmin
                    ? new SpaceAwareUserCountProvider(
                        sp.GetRequiredService<ISpaceAdmin>(),
                        sp.GetRequiredService<ISpaceContextAccessor>(),
                        sp.GetRequiredService<UserRepository>())
                    : new StorageUserCountProvider(sp.GetRequiredService<UserRepository>());
            });

            configure(new IUserManagementBuilder.Builder(builder.Services));

            builder.Services.RegisterModule<UserProfilesModule>();
            builder.Services.RegisterModule<UserAuthenticationModule>();
            builder.Services.RegisterModule<UserMembershipModule>();

            builder.Services.GetOrAddStorageInstanceRouter()
                .AddMapping(DataCategoryName.UserManagement, storageInstanceId);

            // Default user profile schema. Customers replace or extend it with AddInMemoryDataExtensionSchemas,
            // in either order. The profile stays OIDC-shaped even with SCIM (deferred).
            builder.Services.TryAddSchema(BuiltInSchemas.UserProfile);

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

            return builder;
        }

        /// <summary>
        /// Configures IdentityServer to use Duende UserManagement for user profiles, authentication, and membership,
        /// using the default storage instance.
        /// </summary>
        /// <param name="configure">A delegate to configure the user management builder, including storage.</param>
        /// <returns>The same builder instance.</returns>
        public IIdentityServerBuilder AddUserManagement(
            Action<IUserManagementBuilder> configure) => builder.AddUserManagement(StorageInstanceId.Default, configure);
    }
}
