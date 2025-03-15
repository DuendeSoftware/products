// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Stores;

namespace Duende.IdentityServer.Extensions;

/// <summary>
/// Extensions for PersistedGrantFilter.
/// </summary>
public static class PersistedGrantFilterExtensions
{
    /// <summary>
    /// Validates the PersistedGrantFilter and throws if invalid.
    /// </summary>
    /// <param name="filter"></param>
    public static void Validate(this PersistedGrantFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (string.IsNullOrWhiteSpace(filter.ClientId) &&
            filter.ClientIds == null &&
            string.IsNullOrWhiteSpace(filter.SessionId) &&
            string.IsNullOrWhiteSpace(filter.SubjectId) &&
            string.IsNullOrWhiteSpace(filter.Type) &&
            filter.Types == null)
        {
            throw new ArgumentException("No filter values set.", nameof(filter));
        }
    }
}
