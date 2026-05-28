// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using Duende.IdentityServer.Internal.Saml.Sp.Metadata;

namespace Duende.IdentityServer.Internal.Saml.Sp.Helpers
{
    class EntityIdEqualityComparer : IEqualityComparer<EntityId>
    {
        private static EntityIdEqualityComparer instance = new EntityIdEqualityComparer();
        public static EntityIdEqualityComparer Instance
        {
            get
            {
                return instance;
            }
        }

        public bool Equals(EntityId x, EntityId y)
        {
            if (x == null)
            {
                throw new ArgumentNullException(nameof(x));
            }

            if (y == null)
            {
                throw new ArgumentNullException(nameof(y));
            }

            return x.Id == y.Id;
        }

        public int GetHashCode(EntityId obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }
            if (obj.Id == null)
            {
                return 117; // Whatever value, as long as we return the same each time.
            }
            return obj.Id.GetHashCode();
        }
    }
}
