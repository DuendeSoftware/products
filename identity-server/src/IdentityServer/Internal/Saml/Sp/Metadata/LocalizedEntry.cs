// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal abstract class LocalizedEntry
    {
        public string Language { get; set; }

        protected LocalizedEntry()
        {
        }

        protected LocalizedEntry(string language)
        {
            Language = language;
        }
    }
}
