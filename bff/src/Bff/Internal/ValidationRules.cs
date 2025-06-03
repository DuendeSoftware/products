// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

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
}
