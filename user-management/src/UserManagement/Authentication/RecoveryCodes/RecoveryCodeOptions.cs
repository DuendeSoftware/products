// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.RecoveryCodes;

/// <summary>
/// Configuration options for recovery code generation and authentication.
/// </summary>
public sealed class RecoveryCodeOptions
{
    /// <summary>
    /// The number of recovery codes to generate. Defaults to 10.
    /// </summary>
    public int Count { get; set; } = 10;

    /// <summary>
    /// Whether recovery codes are enabled. Defaults to true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
