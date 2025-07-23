// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Configures the dynamic external provider feature.
/// </summary>
public class DynamicProviderOptions
{
    private Dictionary<string, DynamicProviderType> _providers = new Dictionary<string, DynamicProviderType>();

    /// <summary>
    /// Prefix in the pipeline for callbacks from external providers. Defaults to "/federation".
    /// </summary>
    public PathString PathPrefix { get; set; } = "/federation";

    /// <summary>
    /// If set, this callback will be used over <see cref="PathPrefix"/> to determine if the request is for a dynamic provider.
    /// If the request is a match, the callback should return the name of the scheme which matched.
    /// </summary>
    public Func<HttpContext, string?>? PathMatchingCallback { get; set; }

    /// <summary>
    /// Scheme used for signin. Defaults to the constant IdentityServerConstants.ExternalCookieAuthenticationScheme.
    /// </summary>
    public string SignInScheme { get; set; } = IdentityServerConstants.ExternalCookieAuthenticationScheme;

    /// <summary>
    /// Scheme for signout. Defaults to the constant IdentityServerConstants.DefaultCookieAuthenticationScheme.
    /// </summary>
    public string SignOutScheme
    {
        get => _signOutScheme ?? IdentityServerConstants.DefaultCookieAuthenticationScheme;
        set => _signOutScheme = value;
    }

    private string? _signOutScheme;

    /// <summary>
    /// Gets a value indicating if the SignOutScheme was set explicitly, either by application logic or by options binding.
    /// </summary>
    public bool SignOutSchemeSetExplicitly => _signOutScheme != null;

    /// <summary>
    /// Registers a provider configuration model and authentication handler for the protocol type being used.
    /// </summary>
    public void AddProviderType<THandler, TOptions, TIdentityProvider>(string type)
        where THandler : IAuthenticationRequestHandler
        where TOptions : AuthenticationSchemeOptions, new()
        where TIdentityProvider : IdentityProvider
    {
        if (_providers.ContainsKey(type))
        {
            throw new Exception($"Type '{type}' already configured.");
        }

        _providers.Add(type, new DynamicProviderType
        {
            HandlerType = typeof(THandler),
            OptionsType = typeof(TOptions),
            IdentityProviderType = typeof(TIdentityProvider),
        });
    }

    /// <summary>
    /// Finds the DynamicProviderType registration by protocol type.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public DynamicProviderType? FindProviderType(string type) => _providers.GetValueOrDefault(type);

    /// <summary>
    /// Models a provider type registered with the dynamic providers feature.
    /// </summary>
    public class DynamicProviderType
    {
        /// <summary>
        /// The type of the handler.
        /// </summary>
        public Type HandlerType { get; set; } = default!;
        /// <summary>
        /// The type of the options.
        /// </summary>
        public Type OptionsType { get; set; } = default!;
        /// <summary>
        /// The identity provider protocol type.
        /// </summary>
        public Type IdentityProviderType { get; set; } = default!;
    }
}
