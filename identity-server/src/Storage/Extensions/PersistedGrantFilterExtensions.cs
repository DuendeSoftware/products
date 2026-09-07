// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.Extensions;

/// <summary>
/// Extensions for PersistedGrantFilter.
/// </summary>
public static class PersistedGrantFilterExtensions
{
    extension(PersistedGrantFilter filter)
    {
        /// <summary>
        /// Validates the PersistedGrantFilter and throws if invalid.
        /// </summary>
        public void Validate()
        {
            ArgumentNullException.ThrowIfNull(filter);

            if (string.IsNullOrWhiteSpace(filter.ClientId) &&
                filter.ClientIds.Count == 0 &&
                string.IsNullOrWhiteSpace(filter.SessionId) &&
                string.IsNullOrWhiteSpace(filter.SubjectId) &&
                string.IsNullOrWhiteSpace(filter.Type) &&
                filter.Types.Count == 0)
            {
                throw new ArgumentException("No filter values set.", nameof(filter));
            }
        }
    }
}
