// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Internal;

namespace Duende.UserManagement.Authentication.RecoveryCodes;

/// <summary>
/// Represents a plain text recovery code that can be used for account recovery when other authenticators are unavailable.
/// </summary>
[StringValue]
public partial record PlainTextRecoveryCode
{
    private const int NewLength = 10;

    private static readonly byte MaxLength = Pbkdf2MaxPasswordLength.For(new Pbkdf2Inputs().PseudorandomFunctionName);

    /// <summary>Gets the normalized recovery code string value.</summary>
    public string Value { get; }

    /// <summary>
    /// Backward-compatible alias for <see cref="Value"/>.
    /// </summary>
    public string Text => Value;

    /// <summary>
    /// Returns the recovery code as a collection of display groups for user-friendly presentation.
    /// </summary>
    public IReadOnlyCollection<string> ToTextGroups() => [.. Value.ToGroups()];

    /// <inheritdoc />
    public override string ToString() => GetType().ToString();

    internal static PlainTextRecoveryCode New() => new(Base32Crockford.Random(NewLength, false));

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
