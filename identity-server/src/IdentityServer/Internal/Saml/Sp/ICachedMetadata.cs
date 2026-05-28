// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp
{
    interface ICachedMetadata
    {
        /// <summary>
        /// Permitted cache duration for the metadata.
        /// </summary>
        XsdDuration? CacheDuration { get; set; }

        /// <summary>
        /// Valid until
        /// </summary>
        DateTime? ValidUntil { get; set; }
    }
}
