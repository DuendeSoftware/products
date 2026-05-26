// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Otp.Internal;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passwords;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication;

public static class UserAuthenticationBuilderExtensions
{
    extension(IUserAuthenticationBuilder builder)
    {
        public IUserAuthenticationBuilder Configure(Action<UserAuthenticationOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);

            _ = builder.Services.Configure(configure);
            return builder;
        }

        /// <summary>
        /// Configures user authentication to use a custom OTP sender implementation.
        /// </summary>
        /// <typeparam name="TSender">The type of OTP sender to register.</typeparam>
        /// <returns>The builder for chaining.</returns>
        public IUserAuthenticationBuilder UseOtpSender<TSender>()
            where TSender : class, IOtpSender
        {
            ArgumentNullException.ThrowIfNull(builder);

            _ = builder.Services.AddSingleton<IOtpSender, TSender>();

            return builder;
        }

        /// <summary>
        /// Configures user authentication to use the SMTP OTP sender for email.
        /// </summary>
        /// <param name="configure">Configuration action for SMTP OTP sender options.</param>
        /// <returns>The builder for chaining.</returns>
        public IUserAuthenticationBuilder UseSmtpOtpSender(Action<SmtpOtpSenderOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);

            _ = builder.Services
                .AddSingleton<IOtpSender, SmtpOtpSender>()
                .AddSingleton<IEmailContentFactory, EmailContentFactory>()
                .AddOptions<SmtpOtpSenderOptions>()
                .Services
                .AddSingleton<IValidateOptions<SmtpOtpSenderOptions>, SmtpOtpSenderOptionsValidator>()
                .Configure(configure);

            return builder;
        }

        /// <summary>
        /// Registers a custom password validator that will be called during password
        /// set, change, and reset operations. Multiple validators can be registered
        /// and are executed in registration order; the first rejection short-circuits.
        /// </summary>
        /// <typeparam name="TValidator">The type of the password validator to register.</typeparam>
        /// <returns>The builder for chaining.</returns>
        public IUserAuthenticationBuilder AddPasswordValidator<TValidator>()
            where TValidator : class, IPasswordValidator
        {
            ArgumentNullException.ThrowIfNull(builder);

            _ = builder.Services.AddTransient<IPasswordValidator, TValidator>();

            return builder;
        }

        /// <summary>
        /// Registers a custom password hash algorithm that can be used to verify passwords
        /// hashed with that algorithm and to hash new passwords if set as the preferred algorithm.
        /// Multiple algorithms can be registered; the preferred algorithm is configured via
        /// <see cref="PasswordOptions.PreferredHashAlgorithm"/>.
        /// </summary>
        /// <typeparam name="TAlgorithm">The type of the password hash algorithm to register.</typeparam>
        /// <returns>The builder for chaining.</returns>
        public IUserAuthenticationBuilder AddPasswordHashAlgorithm<TAlgorithm>()
            where TAlgorithm : class, IPasswordHashAlgorithm
        {
            ArgumentNullException.ThrowIfNull(builder);

            _ = builder.Services.AddSingleton<IPasswordHashAlgorithm, TAlgorithm>();

            return builder;
        }

        /// <summary>
        /// Registers a custom attestation trust policy that will be called during passkey
        /// registration. Multiple policies can be registered and all must accept; the first
        /// rejection short-circuits registration.
        /// </summary>
        /// <typeparam name="TPolicy">The type of the attestation trust policy to register.</typeparam>
        /// <returns>The builder for chaining.</returns>
        public IUserAuthenticationBuilder AddAttestationTrustPolicy<TPolicy>()
            where TPolicy : class, IAttestationTrustPolicy
        {
            ArgumentNullException.ThrowIfNull(builder);

            _ = builder.Services.AddTransient<IAttestationTrustPolicy, TPolicy>();

            return builder;
        }
    }
}
