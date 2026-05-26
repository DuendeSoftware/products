// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;
using Duende.UserManagement.Authentication.Internal;

namespace Duende.UserManagement.Authentication.Passwords;

/// <summary>
/// Options for configuring the default password validator.
/// </summary>
public sealed class PasswordOptions
{
    /// <summary>
    /// Maximum allowed password length. Defaults to a security-based limit for PBKDF2.
    /// </summary>
    public int MaxLength { get; set; } =
        Pbkdf2MaxPasswordLength.For(new Pbkdf2Inputs().PseudorandomFunctionName);

    /// <summary>
    /// Minimum required password length. Defaults to 8 characters.
    /// </summary>
    public int MinLength { get; set; } = 8;

    /// <summary>
    /// Minimum required lowercase characters. Defaults to 2.
    /// </summary>
    public int MinLower { get; set; } = 2;

    /// <summary>
    /// Minimum required uppercase characters. Defaults to 2.
    /// </summary>
    public int MinUpper { get; set; } = 2;

    /// <summary>
    /// Minimum required numeric digit characters. Defaults to 2.
    /// </summary>
    public int MinDigits { get; set; } = 2;

    /// <summary>
    /// Minimum required symbol characters. Defaults to 2.
    /// </summary>
    public int MinSymbols { get; set; } = 2;

    /// <summary>
    /// The algorithm ID of the preferred password hash algorithm used for new hashes and re-hashing.
    /// Defaults to <c>"pbkdf2"</c>.
    /// </summary>
    [Required]
    public string PreferredHashAlgorithm { get; set; } = Pbkdf2PasswordConstants.AlgorithmId;

    /// <summary>
    /// The number of previous password hashes to retain and check against when a new password is set.
    /// A value of 0 (the default) disables password history checking.
    /// </summary>
    public int HistoryCount { get; set; }

    /// <summary>
    /// The maximum age of a password in days before it is considered expired.
    /// A value of <c>null</c> (the default) disables password expiration.
    /// </summary>
    public int? MaxAgeDays { get; set; }
}
