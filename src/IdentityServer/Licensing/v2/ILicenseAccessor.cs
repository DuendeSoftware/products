// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Licensing.v2;

/// <summary>
/// Provides access to the current License.
/// </summary>
public interface ILicenseAccessor
{
    /// <summary>
    /// Gets the current IdentityServer license.
    /// </summary>
    License Current { get; }
}
