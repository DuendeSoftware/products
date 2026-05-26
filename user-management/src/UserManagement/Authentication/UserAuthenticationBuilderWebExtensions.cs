// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Internal.Passkeys;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement.Authentication;

public static class UserAuthenticationBuilderWebExtensions
{
    extension(IUserAuthenticationBuilder builder)
    {
        public IUserAuthenticationBuilder ConfigureEndpoints(Action<UserAuthenticationEndpointOptions> options)
        {
            _ = builder.Services.Configure(options);
            return builder;
        }

        public IUserAuthenticationBuilder ConfigureEndpoints(IConfigurationSection configurationSection)
        {
            _ = builder.Services.Configure<UserAuthenticationEndpointOptions>(configurationSection);
            return builder;
        }

        /// <summary>
        /// Registers a second-factor passkey authentication resolver and enables the
        /// passkey begin endpoint for second-factor use.
        /// </summary>
        /// <typeparam name="TResolver">
        /// The type of the resolver that identifies the user and their allowed
        /// credentials for the second-factor passkey ceremony.
        /// </typeparam>
        /// <returns>The builder for chaining.</returns>
        public IUserAuthenticationBuilder EnablePasskeyForSecondFactor<TResolver>()
            where TResolver : class, ISecondFactorPasskeyAuthenticationResolver
        {
            ArgumentNullException.ThrowIfNull(builder);

            _ = builder.Services.AddScoped<ISecondFactorPasskeyAuthenticationResolver, TResolver>();
            _ = builder.Services.AddScoped<PasskeyBeginAuthenticationForSecondFactorEndpoint>();

            return builder;
        }

        /// <summary>
        /// Registers a second-factor passkey authentication resolver instance and enables the
        /// passkey begin endpoint for second-factor use.
        /// </summary>
        /// <typeparam name="TResolver">
        /// The type of the resolver that identifies the user and their allowed
        /// credentials for the second-factor passkey ceremony.
        /// </typeparam>
        /// <param name="instance">
        /// The resolver instance to use for second-factor passkey authentication.
        /// </param>
        /// <returns>The builder for chaining.</returns>
        public IUserAuthenticationBuilder EnablePasskeyForSecondFactor<TResolver>(TResolver instance)
            where TResolver : class, ISecondFactorPasskeyAuthenticationResolver
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(instance);

            _ = builder.Services.AddSingleton<ISecondFactorPasskeyAuthenticationResolver>(instance);
            _ = builder.Services.AddScoped<PasskeyBeginAuthenticationForSecondFactorEndpoint>();

            return builder;
        }
    }
}
