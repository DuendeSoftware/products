// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
namespace Duende.IdentityServer.Internal.Saml.Sp
{
    /// <summary>
    /// How should we override the metadata publishing rules
    /// </summary>
    internal enum MetadataPublishOverrideType
    {
        /// <summary>
        /// No override. Published according to the normal rules.
        /// </summary>
        None = 0,

        /// <summary>
        /// Publish as Unspecified
        /// </summary>
        PublishUnspecified = 1,

        /// <summary>
        /// Publish as Encryption
        /// </summary>
        PublishEncryption = 2,

        /// <summary>
        /// Publish as Signing
        /// </summary>
        PublishSigning = 3,

        /// <summary>
        /// Do not publish
        /// </summary>
        DoNotPublish = 4
    }
}
