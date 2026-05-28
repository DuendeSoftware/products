// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using Duende.IdentityServer.Internal.Saml.Sp.Helpers;
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;

namespace Duende.IdentityServer.Internal.Saml.Sp.Configuration
{
    /// <summary>
    /// A thread safe wrapper around a dictionary for the identity providers.
    /// </summary>
    /// <remarks>
    /// First I thought about using a ConcurrentDictionary, but that does not maintain
    /// any order of the added objects. Since the first idp added becomes the default idp,
    /// the order must be preserved. And there has to be queuing semantics if the first idp
    /// is dynamically loaded from a federation and later removed. Locks are simple and
    /// this part of the code shouldn't be that performance sensitive.
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "It works like dictionary, even though it doesn't implement the full interface.")]
    internal class IdentityProviderDictionary
    {
        private Dictionary<EntityId, IdentityProvider> dictionary =
            new Dictionary<EntityId, IdentityProvider>(EntityIdEqualityComparer.Instance);

        /// <summary>
        /// Gets an idp from the entity id.
        /// </summary>
        /// <param name="entityId">entity Id to look up.</param>
        /// <returns>IdentityProvider</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1043:UseIntegralOrStringArgumentForIndexers")]
        public IdentityProvider this[EntityId entityId]
        {
            get
            {
                if (entityId == null)
                {
                    throw new ArgumentNullException(nameof(entityId));
                }

                lock (dictionary)
                {
                    try
                    {
                        return dictionary[entityId];
                    }
                    catch (KeyNotFoundException e)
                    {
                        throw new KeyNotFoundException(
                            "No Idp with entity id \"" + entityId.Id + "\" found.",
                            e);
                    }
                }
            }
            set
            {
                lock (dictionary)
                {
                    dictionary[entityId] = value;
                }
            }
        }

        /// <summary>
        /// Add an identity provider to the collection..
        /// </summary>
        /// <param name="idp">Identity provider to add.</param>
        public void Add(IdentityProvider idp)
        {
            if (idp == null)
            {
                throw new ArgumentNullException(nameof(idp));
            }

            lock (dictionary)
            {
                dictionary.Add(idp.EntityId, idp);
            }
        }

        /// <summary>
        /// The default identity provider; i.e. the first registered of the currently known.
        /// </summary>
        public IdentityProvider Default
        {
            get
            {
                return this[0];
            }
        }

        /// <summary>
        /// Gets all currently known identity providers. Note that the returned
        /// enumeration is a copy to avoid race conditions.
        /// </summary>
        public IEnumerable<IdentityProvider> KnownIdentityProviders
        {
            get
            {
                lock (dictionary)
                {
                    return dictionary.Values.ToList();
                }
            }
        }

        // Used by tests.
        internal IdentityProvider this[int i]
        {
            get
            {
                lock (dictionary)
                {
                    return dictionary.Values.Skip(i).First();
                }
            }
        }

        /// <summary>
        /// Try to get the value of an idp with a given entity id.
        /// </summary>
        /// <param name="idpEntityId">Entity id to search for.</param>
        /// <param name="idp">The idp, if found.</param>
        /// <returns>True if an idp with the given entity id was found.</returns>
        public bool TryGetValue(EntityId idpEntityId, out IdentityProvider idp)
        {
            lock (dictionary)
            {
                return dictionary.TryGetValue(idpEntityId, out idp);
            }
        }

        /// <summary>
        /// Checks if there are no known identity providers.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                lock (dictionary)
                {
                    return dictionary.Count == 0;
                }
            }
        }

        /// <summary>
        /// Removes the idp with the given entity id, if present. If no such
        /// entity is found, nothing is done.
        /// </summary>
        /// <param name="idp">EntityId of idp to remove.</param>
        public void Remove(EntityId idp)
        {
            lock (dictionary)
            {
                dictionary.Remove(idp);
            }
        }
    }
}
