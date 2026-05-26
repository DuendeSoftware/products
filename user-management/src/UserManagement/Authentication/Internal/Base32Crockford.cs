// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;

namespace Duende.UserManagement.Authentication.Internal;

// https://www.crockford.com/base32.html
internal static class Base32Crockford
{
    private const string NumericChars = "0123456789";
    private const string AllChars = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    // Compiled as a reference to static data in the assembly — no heap allocation or per-call creation.
    private static ReadOnlySpan<char> NormalizationTable =>
    [
        ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', // 0-7
        ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', // 8-15
        ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', // 16-23
        ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', // 24-31
        ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', // 32-39
        ' ', ' ', ' ', ' ', ' ', ' ', ' ', ' ', // 40-47
        '0', '1', '2', '3', '4', '5', '6', '7', // 48-55: 0-7
        '8', '9', ' ', ' ', ' ', ' ', ' ', ' ', // 56-63: 8-9
        ' ', 'A', 'B', 'C', 'D', 'E', 'F', 'G', // 64-71: @, A-G
        'H', '1', 'J', 'K', '1', 'M', 'N', '0', // 72-79: H, I=1, J-K, L=1, M-N, O=0
        'P', 'Q', 'R', 'S', 'T', ' ', 'V', 'W', // 80-87: P-T, U=excluded, V-W
        'X', 'Y', 'Z',                          // 88-90: X-Z
    ];

    internal static string Random(int length, bool numericOnly) =>
        string.Create(length, numericOnly ? NumericChars : AllChars, static (span, alphabet) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            }
        });

    /// <summary>
    /// Pure transform: uppercase, map confusable characters (I/L→1, O→0),
    /// and strip hyphens. Does not validate.
    /// </summary>
    internal static string Normalize(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        Span<char> buffer = stackalloc char[input.Length];
        var count = 0;

        foreach (var inputChar in input)
        {
            if (inputChar is '-')
            {
                continue;
            }

            var upper = char.ToUpperInvariant(inputChar);
            if (upper < NormalizationTable.Length)
            {
                var mapped = NormalizationTable[upper];
                if (mapped is not ' ')
                {
                    buffer[count++] = mapped;
                    continue;
                }
            }

            // Unmappable characters pass through — IsValid will reject them.
            buffer[count++] = upper;
        }

        return count == 0 ? string.Empty : new string(buffer[..count]);
    }

    /// <summary>
    /// Validates that every character is in the Crockford Base32 alphabet
    /// and the string does not exceed <paramref name="maxLength"/>.
    /// Expects already-normalized input (uppercase, no hyphens).
    /// </summary>
    internal static bool IsValid(string input, byte maxLength)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Length > maxLength)
        {
            return false;
        }

        foreach (var c in input)
        {
            if (c >= NormalizationTable.Length || NormalizationTable[c] is ' ' || NormalizationTable[c] != c)
            {
                return false;
            }
        }

        return true;
    }
}
