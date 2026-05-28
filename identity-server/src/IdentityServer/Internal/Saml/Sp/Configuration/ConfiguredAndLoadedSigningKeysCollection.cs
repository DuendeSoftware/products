// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Internal.Saml.Sp.Tokens;

namespace Duende.IdentityServer.Internal.Saml.Sp.Configuration
{
    /// <summary>
    /// Collection of items with two sources: configured and loaded dyanically.
    /// The dynamically loaded can reset while the configured are kept.
    /// metadata.
    /// </summary>
    internal class ConfiguredAndLoadedSigningKeysCollection : IEnumerable<SecurityKeyIdentifierClause>
    {
        private IList<SecurityKeyIdentifierClause> configuredItems = new List<SecurityKeyIdentifierClause>();
        private IList<SecurityKeyIdentifierClause> loadedItems = new List<SecurityKeyIdentifierClause>();

        /// <summary>
        /// Add a configured key.
        /// </summary>
        /// <param name="key">Key to add.</param>
        public void AddConfiguredKey(SecurityKeyIdentifierClause key)
        {
            configuredItems.Add(key);
        }

        /// <summary>
        /// Add a configured certificate.
        /// </summary>
        /// <param name="certificate">Certificate to add.</param>
        public void AddConfiguredKey(X509Certificate2 certificate)
        {
            AddConfiguredKey(new X509RawDataKeyIdentifierClause(certificate));
        }

        /// <summary>
        /// Set the complete set of loaded items keys. Previously loaded items
        /// are cleared, configured items remain.
        /// </summary>
        /// <param name="items">Items to set</param>
        public void SetLoadedItems(IList<SecurityKeyIdentifierClause> items)
        {
            loadedItems = items;
        }

        /// <summary>
        /// The loaded items.
        /// </summary>
        public IEnumerable<SecurityKeyIdentifierClause> LoadedItems
        {
            get
            {
                return loadedItems;
            }
        }

        /// <summary>
        /// Gets an enumerator to the combined set of keys.
        /// </summary>
        /// <returns>Enumerator</returns>
        public IEnumerator<SecurityKeyIdentifierClause> GetEnumerator()
        {
            return configuredItems.Union(loadedItems).GetEnumerator();
        }

        // Weakly typed method only exists because interface requires it,
        // ignore in code coverage.
        [ExcludeFromCodeCoverage()]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
