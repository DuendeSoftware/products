// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Internal;
using Duende.UserManagement.Internal.Modules;
using Duende.UserManagement.Membership.Internal;
using Duende.UserManagement.Profiles.Internal;
using Duende.UserManagement.Scim;
using Duende.UserManagement.Scim.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement;

public static class UserManagementBuilderExtensions
{
    extension(IUserManagementBuilder builder)
    {
        public IUserManagementBuilder EnableProfiles()
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Services.RegisterModule<UserProfilesModule>();
            return builder;
        }

        public IUserManagementBuilder EnableAuthentication()
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Services.RegisterModule<UserAuthenticationModule>();
            return builder;
        }

        public IUserManagementBuilder EnableAuthentication(Action<IUserAuthenticationBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);
            builder.Services.RegisterModule<UserAuthenticationModule>();
            configure(new IUserAuthenticationBuilder.FeatureBuilder(builder.Services));
            return builder;
        }

        public IUserManagementBuilder EnableMembership()
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Services.RegisterModule<UserMembershipModule>();
            return builder;
        }

        [Experimental(diagnosticId: "duende_experimental",
            Message = "SCIM support is experimental and may change in future releases.")]
        public IUserManagementBuilder EnableScim()
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Services.RegisterModule<ScimModule>();
            return builder;
        }

        [Experimental(diagnosticId: "duende_experimental",
            Message = "SCIM support is experimental and may change in future releases.")]
        public IUserManagementBuilder EnableScim(Action<ScimOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configureOptions);
            builder.Services.RegisterModule<ScimModule>();
            return builder;
        }


        [Experimental(diagnosticId: "duende_experimental",
            Message = "SCIM support is experimental and may change in future releases.")]
        public IUserManagementBuilder EnableScim(Action<ScimOptions> configureOptions,
            Action<ScimEndpointOptions> configureEndpoints)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configureOptions);
            ArgumentNullException.ThrowIfNull(configureEndpoints);
            builder.Services.RegisterModule<ScimModule>();
            _ = builder.Services.Configure(configureOptions);
            _ = builder.Services.Configure(configureEndpoints);
            return builder;
        }
    }
}
