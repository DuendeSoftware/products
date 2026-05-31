// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Otp;

/// <summary>
/// Represents the content of an OTP email message, including subject, body, and whether the body is HTML.
/// </summary>
public sealed record EmailContent
{
    internal EmailContent(string subject, string body, bool isBodyHtml)
    {
        Subject = subject;
        Body = body;
        IsBodyHtml = isBodyHtml;
    }

    /// <summary>Gets the email subject line.</summary>
    public string Subject { get; }

    /// <summary>Gets the email body text.</summary>
    public string Body { get; }

    /// <summary>Gets a value indicating whether the body is HTML.</summary>
    public bool IsBodyHtml { get; }

    /// <summary>
    /// Deconstructs the email content into its component parts.
    /// </summary>
    /// <param name="subject">The email subject.</param>
    /// <param name="body">The email body.</param>
    /// <param name="isBodyHtml">Whether the body is HTML.</param>
    public void Deconstruct(out string subject, out string body, out bool isBodyHtml)
    {
        subject = Subject;
        body = Body;
        isBodyHtml = IsBodyHtml;
    }
}
