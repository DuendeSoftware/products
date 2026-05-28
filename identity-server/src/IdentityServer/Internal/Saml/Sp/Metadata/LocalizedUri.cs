// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp.Metadata
{
    internal class LocalizedUri : LocalizedEntry
    {
        public Uri Uri { get; set; }

        public LocalizedUri(Uri uri, string language) :
            base(language)
        {
            Uri = uri;
        }

        public LocalizedUri() :
            this(null, null)
        {
        }
    }
}
