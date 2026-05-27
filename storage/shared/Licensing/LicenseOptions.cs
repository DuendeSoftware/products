// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Licensing.Enforcement;

/// <summary>
/// Options for configuring the Duende license.
/// </summary>
public sealed class LicenseOptions
{
    /// <summary>
    /// The license key string (JWT). If set, this takes precedence over file-based discovery.
    /// </summary>
    public string? LicenseKey { get; set; }

    /// <summary>
    /// Path to a file containing the license key. Used when <see cref="LicenseKey"/> is not set.
    /// If neither is configured, the library falls back to auto-discovery of
    /// Duende_License.key in the working directory.
    /// </summary>
    public string? LicenseKeyPath { get; set; }
}
