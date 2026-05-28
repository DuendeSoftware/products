// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
using System.Collections.ObjectModel;

namespace Duende.IdentityServer.Internal.Saml.Sp.Tokens
{
    internal enum AudienceUriMode
    {
        Always,
        BearerKeyOnly,
        Never
    }

    internal class AudienceRestriction
    {
        public AudienceUriMode AudienceMode { get; set; } = AudienceUriMode.Always;
        public Collection<Uri> AllowedAudienceUris { get; } = new Collection<Uri>();

        public AudienceRestriction()
        {
        }

        public AudienceRestriction(AudienceUriMode audienceMode)
        {
            AudienceMode = audienceMode;
        }
    }
}
