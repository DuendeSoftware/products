// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Tokens
{
    internal class KeyNameIdentifierClause : SecurityKeyIdentifierClause
    {
        public KeyNameIdentifierClause(string keyName) :
            base(null)
        {
            KeyName = keyName;
        }

        public string KeyName { get; private set; }

        public override bool Matches(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            if (keyIdentifierClause == null)
            {
                throw new ArgumentNullException(nameof(keyIdentifierClause));
            }
            return keyIdentifierClause is KeyNameIdentifierClause otherClause &&
                Matches(otherClause.KeyName);
        }

        public bool Matches(string keyName)
        {
            return KeyName == keyName;
        }

        public override string ToString()
        {
            return $"KeyNameIdentifierClause(KeyName = '{KeyName}')";
        }
    }
}
