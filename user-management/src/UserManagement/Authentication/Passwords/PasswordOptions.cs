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
    /// Minimum required password length. Defaults to 15 characters.
    /// </summary>
    /// <remarks>
    /// NIST SP 800-63B-4 section 3.1.1.2 (item 1) requires at least 15 characters for passwords used as a
    /// single-factor authenticator. A minimum of 8 is permitted only for passwords used exclusively as part of
    /// multi-factor authentication. Because password-only authentication is supported, the default is 15.
    /// See https://pages.nist.gov/800-63-4/sp800-63b/authenticators/#passwordver.
    /// </remarks>
    public int MinLength { get; set; } = 15;

    // NIST SP 800-63B-4 section 3.1.1.2 (item 5) prohibits composition rules such as requiring a mixture of
    // character types, so the composition minimums below default to 0. Nonzero values remain configurable for
    // hosts with their own requirements, but do not follow this guidance.
    // https://pages.nist.gov/800-63-4/sp800-63b/authenticators/#passwordver

    /// <summary>
    /// Minimum required lowercase characters. Defaults to 0.
    /// </summary>
    public int MinLower { get; set; }

    /// <summary>
    /// Minimum required uppercase characters. Defaults to 0.
    /// </summary>
    public int MinUpper { get; set; }

    /// <summary>
    /// Minimum required numeric digit characters. Defaults to 0.
    /// </summary>
    public int MinDigits { get; set; }

    /// <summary>
    /// Minimum required symbol characters. Defaults to 0.
    /// </summary>
    public int MinSymbols { get; set; }

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
