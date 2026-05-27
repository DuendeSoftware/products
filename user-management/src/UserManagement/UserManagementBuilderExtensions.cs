// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Scim;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement;

public static class UserManagementBuilderExtensions
{
    extension(IUserManagementBuilder builder)
    {
        public IUserManagementBuilder Authentication(Action<IUserAuthenticationBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);
            configure(new IUserAuthenticationBuilder.FeatureBuilder(builder.Services));
            return builder;
        }

        [Experimental(diagnosticId: "duende_experimental",
            Message = "SCIM support is experimental and may change in future releases.")]
        public IUserManagementBuilder Scim(Action<ScimOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configureOptions);
            _ = builder.Services.Configure(configureOptions);
            return builder;
        }

        [Experimental(diagnosticId: "duende_experimental",
            Message = "SCIM support is experimental and may change in future releases.")]
        public IUserManagementBuilder Scim(Action<ScimOptions> configureOptions,
            Action<ScimEndpointOptions> configureEndpointOptions)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configureOptions);
            ArgumentNullException.ThrowIfNull(configureEndpointOptions);
            _ = builder.Services.Configure(configureOptions);
            _ = builder.Services.Configure(configureEndpointOptions);
            return builder;
        }
    }
}
