// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Internal.Saml.Sp;
using Duende.IdentityServer.Saml.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Saml2HandlerOptions = Duende.IdentityServer.Internal.Saml.Sp.AspNetCore.Saml2Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the SAML 2.0 Service Provider handler on
/// <see cref="AuthenticationBuilder"/> without requiring the dynamic provider
/// infrastructure.
/// </summary>
public static class AuthenticationBuilderSamlServiceProviderExtensions
{
    /// <summary>
    /// Registers a standalone SAML 2.0 Service Provider authentication handler
    /// with the default scheme name (<see cref="SamlServiceProviderDefaults.Scheme"/>).
    /// </summary>
    /// <param name="builder">The authentication builder.</param>
    /// <param name="configure">Action to configure the <see cref="SamlServiceProviderOptions"/>.</param>
    /// <returns>The builder, for chaining.</returns>
    public static AuthenticationBuilder AddSamlServiceProvider(
        this AuthenticationBuilder builder,
        Action<SamlServiceProviderOptions> configure) =>
        builder.AddSamlServiceProvider(SamlServiceProviderDefaults.Scheme, configure);

    /// <summary>
    /// Registers a standalone SAML 2.0 Service Provider authentication handler
    /// with a custom scheme name.
    /// </summary>
    /// <param name="builder">The authentication builder.</param>
    /// <param name="scheme">The authentication scheme name.</param>
    /// <param name="configure">Action to configure the <see cref="SamlServiceProviderOptions"/>.</param>
    /// <returns>The builder, for chaining.</returns>
    public static AuthenticationBuilder AddSamlServiceProvider(
        this AuthenticationBuilder builder,
        string scheme,
        Action<SamlServiceProviderOptions> configure) =>
        builder.AddSamlServiceProvider(scheme, SamlServiceProviderDefaults.DisplayName, configure);

    /// <summary>
    /// Registers a standalone SAML 2.0 Service Provider authentication handler
    /// with a custom scheme name and display name.
    /// </summary>
    /// <param name="builder">The authentication builder.</param>
    /// <param name="scheme">The authentication scheme name.</param>
    /// <param name="displayName">The display name for the authentication scheme.</param>
    /// <param name="configure">Action to configure the <see cref="SamlServiceProviderOptions"/>.</param>
    /// <returns>The builder, for chaining.</returns>
    public static AuthenticationBuilder AddSamlServiceProvider(
        this AuthenticationBuilder builder,
        string scheme,
        string displayName,
        Action<SamlServiceProviderOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        // Ensure TimeProvider is available (normally registered by AddIdentityServer,
        // but this extension lives on AuthenticationBuilder and may be used independently)
        builder.Services.TryAddSingleton(TimeProvider.System);

        // Register named options so each scheme gets isolated configuration
        builder.Services.Configure(scheme, configure);

        // Validate required properties eagerly at host startup
        builder.Services.AddSingleton<IValidateOptions<SamlServiceProviderOptions>>(
            new SamlServiceProviderOptionsValidator(scheme));
        builder.Services.AddOptions<SamlServiceProviderOptions>(scheme)
            .ValidateOnStart();

        // Map the public SamlServiceProviderOptions → internal Saml2Options (scheme-aware)
        builder.Services.AddSingleton<IConfigureOptions<Saml2HandlerOptions>>(sp =>
            new ConfigureSaml2OptionsFromServiceProvider(
                scheme,
                sp.GetRequiredService<IOptionsMonitor<SamlServiceProviderOptions>>(),
                sp.GetRequiredService<TimeProvider>()));

        // Delegate to the existing internal AddSaml2 registration
        builder.AddSaml2(scheme, displayName, _ => { });

        return builder;
    }
}
