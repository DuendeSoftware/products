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
