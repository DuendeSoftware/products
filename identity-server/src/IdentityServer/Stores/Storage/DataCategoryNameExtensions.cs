// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.Storage;

namespace Duende.IdentityServer.Stores.Storage;

/// <summary>
/// Defines the IdentityServer-specific storage categories. Configuration and operational data are
/// routed to the <see cref="StorageInstanceId"/> each category is mapped to, or to
/// <see cref="StorageInstanceId.Default"/> when the category is not mapped.
/// </summary>
public static class DataCategoryNameExtensions
{
    extension(DataCategoryName)
    {
        /// <summary>
        /// The storage category for IdentityServer's configuration data (clients, identity
        /// providers, SAML service providers, resources, and scopes).
        /// </summary>
        public static DataCategoryName Configuration => DataCategoryName.Create("configuration");

        /// <summary>
        /// The storage category for IdentityServer's operational data (persisted grants, device
        /// flow, pushed authorization requests, server-side sessions, signing keys, SAML signin
        /// state, and SAML logout sessions).
        /// </summary>
        public static DataCategoryName Operational => DataCategoryName.Create("operational");
    }
}
