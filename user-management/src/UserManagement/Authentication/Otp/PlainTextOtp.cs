// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Internal;

namespace Duende.UserManagement.Authentication.Otp;

/// <summary>
/// Represents a plain text OTP (One-Time Password) code submitted by a user for verification.
/// </summary>
[StringValue]
public partial record PlainTextOtp
{
    // Eight Crockford Base32 characters provide 40 bits of entropy, a product choice above the minimum
    // for verifier-generated out-of-band secrets (not authenticator-generated OTPs such as TOTP).
    // NIST SP 800-63B-4 section 3.1.3.2 requires at least six decimal digits (or equivalent) and
    // rate limiting for secrets below 64 bits:
    // https://pages.nist.gov/800-63-4/sp800-63b/authenticators/#oobver
    // This comparison does not establish flow compliance: section 3.1.3.1 prohibits email for
    // out-of-band authentication; email-address confirmation and issued recovery codes are separate:
    // https://pages.nist.gov/800-63-4/sp800-63b/authenticators/#ooba
    private const int NewLength = 8;
    private const bool NumericOnly = false;

    private static readonly byte MaxLength = Pbkdf2MaxPasswordLength.For(new Pbkdf2Inputs().PseudorandomFunctionName);

    /// <summary>Gets the normalized OTP string value.</summary>
    public string Value { get; }

    /// <summary>
    /// Backward-compatible alias for <see cref="Value"/>.
    /// </summary>
    public string Text => Value;

    /// <summary>
    /// Returns the OTP code as a collection of display groups for user-friendly presentation.
    /// </summary>
    public IReadOnlyCollection<string> ToTextGroups() => [.. Value.ToGroups()];

    /// <summary>Returns a redacted string to prevent accidental logging of OTP values.</summary>
    public override string ToString() => GetType().ToString();

    internal static PlainTextOtp New() => new(Base32Crockford.Random(NewLength, NumericOnly));

    static string Normalize(string value) =>
        Base32Crockford.Normalize(value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal));

    static bool TryValidate(string s, out IReadOnlyList<string>? errors)
    {
        errors = null;
        if (!Base32Crockford.IsValid(s, MaxLength))
        {
            errors = ["The value is not a valid Base32 Crockford string."];
            return false;
        }

        return true;
    }
}
