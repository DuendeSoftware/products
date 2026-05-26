// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement;

// https://www.rfc-editor.org/rfc/rfc9493.html#name-email-identifier-format
[StringValue]
public partial record EmailAddress : ISubjectId
{
    // x@y
    private const int MinLength = 3;

    // Max username length is 64 octets - https://datatracker.ietf.org/doc/html/rfc5321#section-4.5.3.1.1
    // "@" is 1 octet
    // Max domain name length is 255 octets - https://datatracker.ietf.org/doc/html/rfc5321#section-4.5.3.1.2
    // 64 + 1 + 255 = 320
    // This is complicated by ambiguity in whether 2 octet (Unicode) chars are allowed.
    // E.g. System.Net.Mail.MailAddress allows them even though the documentation says it doesn't
    // - https://learn.microsoft.com/en-us/dotnet/api/system.net.mail.mailaddress.-ctor?view=net-9.0#system-net-mail-mailaddress-ctor(system-string-system-string)
    // We don't want to be restrictive
    // (the only true test of whether an email address is valid is the sending of an email to that address),
    // so we will allow Unicode chars and assume that implementations allow 320 Unicode chars (640 octets).
    // Not that we really care about the exact number,
    // but we don't want people flooding the store with enormous email addresses,
    // so we may as well use some reasonable number.
    internal const int MaxLength = 320;

    private static string Normalize(string input) => input.Trim();

    private static bool TryValidate(string input, out IReadOnlyList<string>? errors)
    {
        errors = null;

        if (input.Length < MinLength)
        {
            errors = [$"An email address must be at least {MinLength} characters."];
            return false;
        }

        if (!input[1..^1].Contains('@', StringComparison.InvariantCulture))
        {
            errors = ["An email address must contain an '@' character."];
            return false;
        }

        return true;
    }


}
