// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Configuration
{
    /// <summary>
    /// Options implementation for handling in memory options.
    /// </summary>
    internal class Options : IOptions
    {
        /// <summary>
        /// Set of callbacks that can be used as extension points for various
        /// events.
        /// </summary>
        public Saml2Notifications Notifications { get; set; }

        /// <summary>
        /// Creates an options object with the specified SPOptions.
        /// </summary>
        /// <param name="spOptions"></param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "sp")]
        public Options(SPOptions spOptions)
        {
            Notifications = new Saml2Notifications();
            SPOptions = spOptions;
            if (SPOptions.Logger == null)
            {
                SPOptions.Logger = new NullLoggerAdapter();
            }
        }

        /// <summary>
        /// Options for the service provider's behaviour; i.e. everything except
        /// the idp and federation list.
        /// </summary>
        public SPOptions SPOptions { get; }

        private readonly IdentityProviderDictionary identityProviders = new IdentityProviderDictionary();

        /// <summary>
        /// Available identity providers.
        /// </summary>
        public IdentityProviderDictionary IdentityProviders
        {
            get
            {
                return identityProviders;
            }
        }
    }
}
