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

        if (String.IsNullOrWhiteSpace(filter.ClientId) &&
            filter.ClientIds == null &&
            String.IsNullOrWhiteSpace(filter.SessionId) &&
            String.IsNullOrWhiteSpace(filter.SubjectId) &&
            String.IsNullOrWhiteSpace(filter.Type) &&
            filter.Types == null)
        {
            throw new ArgumentException("No filter values set.", nameof(filter));
        }
    }
}