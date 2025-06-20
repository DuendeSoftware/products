// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.RegularExpressions;

namespace Duende.Bff.Internal;

internal static partial class ValidationRules
{
    public static ValidationRule<string> MaxLength(int maxLength) =>
        (string s, out string message) =>
        {
            var isValid = s.Length <= maxLength;
            message = !isValid ? $"The string exceeds maximum length {maxLength}." : string.Empty;

            return isValid;
        };

    [GeneratedRegex("^[a-zA-Z0-9_\\-:/\\.\\+]*$")]
    private static partial Regex AlphaNumericDashRegex();

    public static ValidationRule<string> AlphaNumericOrSelectSeparators() =>
        (string s, out string message) =>
        {
            var isValid = AlphaNumericDashRegex().IsMatch(s);
            message = !isValid ? "The string must be alphanumeric or one of these characters: '_-+:/'. " : string.Empty;

            return isValid;
        };
}
