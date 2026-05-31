// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Scim;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement;

/// <summary>
/// Extension methods for <see cref="IUserManagementBuilder"/> to enable user management features.
/// </summary>
public static class UserManagementBuilderExtensions
{
    extension(IUserManagementBuilder builder)
    {
        /// <summary>
        /// Enables user authentication support with custom configuration.
        /// </summary>
        /// <param name="configure">A delegate to configure the authentication builder.</param>
        /// <returns>The builder for chaining.</returns>
        public IUserManagementBuilder Authentication(Action<IUserAuthenticationBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);
            configure(new IUserAuthenticationBuilder.FeatureBuilder(builder.Services));
            return builder;
        }

        /// <summary>
        /// Enables SCIM support with custom SCIM options.
        /// </summary>
        /// <param name="configureOptions">A delegate to configure <see cref="ScimOptions"/>.</param>
        /// <returns>The builder for chaining.</returns>
        [Experimental(diagnosticId: "duende_experimental",
            Message = "SCIM support is experimental and may change in future releases.")]
        public IUserManagementBuilder Scim(Action<ScimOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configureOptions);
            _ = builder.Services.Configure(configureOptions);
            return builder;
        }

        /// <summary>
        /// Enables SCIM support with custom SCIM options and endpoint options.
        /// </summary>
        /// <param name="configureOptions">A delegate to configure <see cref="ScimOptions"/>.</param>
        /// <param name="configureEndpointOptions">A delegate to configure <see cref="ScimEndpointOptions"/>.</param>
        /// <returns>The builder for chaining.</returns>
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
