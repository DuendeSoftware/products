// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using Duende.IdentityServer.Internal.Saml.Sp.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Duende.IdentityServer.Internal.Saml.Sp.AspNetCore
{
    /// <summary>
    /// Options for Saml2 Authentication
    /// </summary>
    internal class Saml2Options : AuthenticationSchemeOptions, IOptions
    {
        /// <summary>
        /// Ctor
        /// </summary>
        public Saml2Options()
        {
            SPOptions = new SPOptions()
            {
                ModulePath = "/Saml2"
            };
        }

        /// <summary>
        /// Authentication scheme to sign in with to establish a session after
        /// the SAML2 authentication is done.
        /// </summary>
        public string SignInScheme { get; set; }


        /// <summary>
        /// Authentication scheme to sign out with when a logout requerst is
        /// received from the idp.
        /// </summary>
        public string SignOutScheme { get; set; }

        /// <summary>
        /// Options for the service provider's behaviour; i.e. everything except
        /// the idp list and the notifications.
        /// </summary>
        public SPOptions SPOptions { get; set; }

        /// <summary>
        /// Cookie manager for reading/writing cookies
        /// </summary>
        public ICookieManager CookieManager { get; set; }

        /// <summary>
        /// Information about known identity providers.
        /// </summary>
        public IdentityProviderDictionary IdentityProviders { get; }
            = new IdentityProviderDictionary();

        /// <summary>
        /// Set of callbacks that can be used as extension points for various
        /// events.
        /// </summary>
        public Saml2Notifications Notifications { get; }
            = new Saml2Notifications();
    }
}
