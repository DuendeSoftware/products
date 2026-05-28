// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Configuration
{
    /// <summary>
    /// Root interface for the options objects, handling all configuration of
    /// Saml2.
    /// </summary>
    internal interface IOptions
    {
        /// <summary>
        /// Options for the service provider's behaviour; i.e. everything except
        /// the idp list and the notifications.
        /// </summary>
        SPOptions SPOptions { get; }

        /// <summary>
        /// Information about known identity providers.
        /// </summary>
        IdentityProviderDictionary IdentityProviders { get; }

        /// <summary>
        /// Set of callbacks that can be used as extension points for various
        /// events.
        /// </summary>
        Saml2Notifications Notifications { get; }
    }
}
