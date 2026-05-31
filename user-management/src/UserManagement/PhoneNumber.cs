// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement;

// https://www.rfc-editor.org/rfc/rfc9493.html#name-phone-number-identifier-for
/// <summary>
/// Represents a phone number subject identifier as defined by RFC 9493.
/// </summary>
[StringValue]
public partial record PhoneNumber : ISubjectId
{
    // https://www.itu.int/rec/dologin_pub.asp?lang=e&id=T-REC-E.164-201011-I!!PDF-E&type=items section 6.2.1
    // Not that we really care about the exact number,
    // but we don't want people flooding the store with enormous phone numbers,
    // so we may as well use some reasonable number.
    internal const int MaxLength = 15;

    private static string Normalize(string input) =>
        new string(input.Trim().TrimStart('+', '0').Where(c => !char.IsWhiteSpace(c)).ToArray());

    private static bool TryValidate(string input, out IReadOnlyList<string>? errors)
    {
        errors = null;

        if (input.Any(c => !char.IsDigit(c)))
        {
            errors = ["A phone number must contain only digits (after stripping leading '+' or '0' and whitespace)."];
            return false;
        }

        return true;
    }


}
