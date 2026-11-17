// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.UserManagement.Authentication;
using Duende.UserManagement.Internal.Modules;
using Duende.UserManagement.Scim;
using Duende.UserManagement.Scim.Internal;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
        public IUserManagementBuilder EnableScim(Action<ScimOptions> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configureOptions);
            _ = builder.Services.Configure(configureOptions);
            builder.Services.TryAddSingleton<ScimEnabledMarker>();
            RegisterScim(builder.Services);
            return builder;
        }

        /// <summary>
        /// Enables SCIM support with custom SCIM options and endpoint options.
        /// </summary>
        /// <param name="configureOptions">A delegate to configure <see cref="ScimOptions"/>.</param>
        /// <param name="configureEndpointOptions">A delegate to configure <see cref="ScimEndpointOptions"/>.</param>
        /// <returns>The builder for chaining.</returns>
        public IUserManagementBuilder EnableScim(Action<ScimOptions> configureOptions,
            Action<ScimEndpointOptions> configureEndpointOptions)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configureOptions);
            ArgumentNullException.ThrowIfNull(configureEndpointOptions);
            _ = builder.Services.Configure(configureOptions);
            _ = builder.Services.Configure(configureEndpointOptions);
            builder.Services.TryAddSingleton<ScimEnabledMarker>();
            RegisterScim(builder.Services);
            return builder;
        }

        /// <summary>
        /// Configures SCIM endpoint OAuth options (authority, audience, etc.).
        /// </summary>
        /// <param name="configure">A delegate to configure <see cref="ScimOAuthOptions"/>.</param>
        /// <returns>The builder for chaining.</returns>
        public IUserManagementBuilder ConfigureScimOAuth(Action<ScimOAuthOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);

            _ = builder.Services.Configure(configure);

            return builder;
        }
    }

    private static void RegisterScim(IServiceCollection services)
    {
        // Register the SCIM module (scheme, endpoints, authorization policies) before the
        // in-process validation below, so its IConfigureOptions<JwtBearerOptions> registrations
        // (which land on the scheme AddJwtBearer registers here) run after AddJwtBearer's own
        // empty configure delegate for the scheme.
        services.RegisterModule<ScimModule>();

        services.TryAddSingleton<IValidateOptions<ScimOAuthOptions>, ScimOAuthOptionsValidator>();
        _ = services.AddOptions<ScimOAuthOptions>().ValidateOnStart();

        // In-process SCIM token validation: when ScimOAuthOptions.Authority is unset, bind the
        // DuendeScimBearer scheme to the current request's issuer and signing keys, replacing
        // the old IssuerUri-to-Authority post-configure (removed). The marker lets the options
        // validator skip the Authority requirement.
        services.AddHttpContextAccessor();
        services.TryAddSingleton<ScimLocalIssuerConfigurationManager>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfigureOptions<JwtBearerOptions>, ScimLocalIssuerJwtBearerConfigureOptions>());
        services.TryAddSingleton<ScimLocalIssuerMarker>();
    }
}
