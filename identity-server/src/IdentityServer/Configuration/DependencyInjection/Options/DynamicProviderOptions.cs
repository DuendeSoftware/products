// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Hosting.DynamicProviders.Store;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Settings for the dynamic external identity provider feature, which allows identity providers
/// to be configured at runtime without restarting the application.
/// </summary>
public class DynamicProviderOptions
{
    private Dictionary<string, DynamicProviderType> _providers = new Dictionary<string, DynamicProviderType>();

    /// <summary>
    /// Gets or sets the path prefix used for external provider callback URLs in the ASP.NET Core pipeline.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>"/federation"</c>. Callback URLs for dynamic providers are constructed
    /// as <c>{PathPrefix}/{scheme}/signin</c> (and similar patterns).
    /// </remarks>
    public PathString PathPrefix { get; set; } = "/federation";

    /// <summary>
    /// Gets or sets an optional callback used to determine whether an incoming request targets a dynamic
    /// provider. When set, this takes precedence over <see cref="PathPrefix"/>.
    /// </summary>
    /// <remarks>
    /// The callback receives the current <see cref="HttpContext"/> and should return the name
    /// of the matching authentication scheme, or <c>null</c> if the request is not for a
    /// dynamic provider.
    /// </remarks>
    public Func<HttpContext, Task<string?>>? PathMatchingCallback { get; set; }

    /// <summary>
    /// Gets or sets the authentication scheme used to sign in users after a successful external
    /// authentication.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="IdentityServerConstants.ExternalCookieAuthenticationScheme"/>
    /// (<c>"idsrv.external"</c>).
    /// </remarks>
    public string SignInScheme { get; set; } = IdentityServerConstants.ExternalCookieAuthenticationScheme;

    /// <summary>
    /// Gets or sets the authentication scheme used to sign out users from external providers.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="IdentityServerConstants.DefaultCookieAuthenticationScheme"/>
    /// (<c>"idsrv"</c>).
    /// </remarks>
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
    /// Gets or sets how long cached authentication handler options and identity provider records for a
    /// dynamic provider in a given space may go unused before they are released.
    /// </summary>
    /// <remarks>
    /// Defaults to one hour. An entry for a <c>(space, scheme)</c> combination that is not used again
    /// within this time is removed from the dynamic provider caches and rebuilt from the store the next
    /// time it is needed. This bounds the memory the caches use across many spaces, including spaces
    /// that were deleted or are simply idle. Reclamation runs during request processing, at most once
    /// per quarter of this timeout, on every request handled by <c>IdentityServerMiddleware</c> — not
    /// only ones that resolve a dynamic scheme, so idle entries are reclaimed even while only
    /// static-only, local-login, token, or other-provider-type traffic arrives. Set to
    /// <see cref="Timeout.InfiniteTimeSpan"/> to disable expiry and keep every entry indefinitely. Must
    /// be a positive value or <see cref="Timeout.InfiniteTimeSpan"/>.
    /// </remarks>
    public TimeSpan CacheIdleTimeout
    {
        get => _cacheIdleTimeout;
        set
        {
            if (value != Timeout.InfiniteTimeSpan && value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    $"{nameof(CacheIdleTimeout)} must be greater than zero or {nameof(Timeout)}.{nameof(Timeout.InfiniteTimeSpan)}.");
            }

            _cacheIdleTimeout = value;
        }
    }

    private TimeSpan _cacheIdleTimeout = TimeSpan.FromHours(1);

    /// <summary>
    /// Registers a provider configuration model and authentication handler for the protocol type being used.
    /// </summary>
    public void AddProviderType<THandler, TOptions, TIdentityProvider>(string type)
        where THandler : IAuthenticationRequestHandler
        where TOptions : AuthenticationSchemeOptions, new()
        where TIdentityProvider : IdentityProvider, new()
    {
        if (_providers.ContainsKey(type))
        {
            throw new InvalidOperationException($"Type '{type}' already configured.");
        }

        var identityProviderType = typeof(TIdentityProvider);
        var ctor = identityProviderType.GetConstructor([typeof(IdentityProvider)]);
        if (ctor == null)
        {
            throw new InvalidOperationException(
                $"The identity provider type '{identityProviderType.FullName}' must have a " +
                $"copy constructor with signature 'ctor({nameof(IdentityProvider)})' to be " +
                $"used as a dynamic provider type.");
        }

        _providers.Add(type, new DynamicProviderType
        {
            HandlerType = typeof(THandler),
            OptionsType = typeof(TOptions),
            IdentityProviderType = identityProviderType,
            CopyConstructor = (baseModel) => (IdentityProvider)ctor.Invoke([baseModel]),
            HasSpaceAwareOptionsCache = services => services.GetService<IOptionsMonitorCache<TOptions>>() is ISpaceAwareDynamicOptionsCache,
        });
    }

    /// <summary>
    /// Finds the DynamicProviderType registration by protocol type.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    public DynamicProviderType? FindProviderType(string type) => _providers.GetValueOrDefault(type);

    /// <summary>
    /// Gets all registered provider type mappings (protocol type string to provider metadata).
    /// </summary>
    internal IReadOnlyDictionary<string, DynamicProviderType> ProviderTypes => _providers;

    /// <summary>
    /// Models a provider type registered with the dynamic providers feature.
    /// </summary>
    public class DynamicProviderType
    {
        /// <summary>
        /// Gets or sets the type of the handler.
        /// </summary>
        public Type HandlerType { get; set; } = default!;
        /// <summary>
        /// Gets or sets the type of the options.
        /// </summary>
        public Type OptionsType { get; set; } = default!;
        /// <summary>
        /// Gets or sets the identity provider protocol type.
        /// </summary>
        public Type IdentityProviderType { get; set; } = default!;
        /// <summary>
        /// Gets or sets a cached delegate that invokes the copy constructor
        /// to create a derived <see cref="IdentityProvider"/> from a base instance.
        /// </summary>
        internal Func<IdentityProvider, IdentityProvider> CopyConstructor { get; set; } = default!;

        /// <summary>
        /// Gets or sets a delegate that reports whether the options cache registered for
        /// <see cref="OptionsType"/> keeps dynamic provider options separate per space.
        /// </summary>
        internal Func<IServiceProvider, bool> HasSpaceAwareOptionsCache { get; set; } = default!;
    }
}
