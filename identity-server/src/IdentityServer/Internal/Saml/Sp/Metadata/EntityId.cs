// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class EntityId
    {
        public string Id { get; set; }

        public EntityId(string id)
        {
            Id = id;
        }

        public EntityId() :
            this(null)
        {
        }
    }
}
