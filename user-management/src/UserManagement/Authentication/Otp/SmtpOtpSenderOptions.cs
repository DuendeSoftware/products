// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace Duende.UserManagement.Authentication.Otp;

public sealed class SmtpOtpSenderOptions
{
    [Required]
    public string Host { get; set; } = null!;

    public int Port { get; set; } = 1025;

    public bool EnableSsl { get; set; } = true;

    [Required]
    public string FromEmail { get; set; } = null!;

    [Required]
    public string FromName { get; set; } = null!;

    /// <summary>
    /// The domain/website where the code should be entered (e.g., "example.com" or "https://example.com").
    /// If not set, domain information will not be included in the email.
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// Custom plain text template for the email body.
    /// Available placeholders: {Code}, {FromName}, {ExpiresMinutes}, {Domain}.
    /// If not set, the default template with security warnings will be used.
    /// </summary>
    public string? PlainTextTemplate { get; set; }

    /// <summary>
    /// Custom HTML template for the email body.
    /// Available placeholders: {Code}, {FromName}, {ExpiresMinutes}, {Domain}.
    /// If set, the email will be sent as HTML instead of plain text.
    /// </summary>
    public string? HtmlTemplate { get; set; }

    /// <summary>
    /// Custom subject template for the email.
    /// Available placeholders: {FromName}, {Code}.
    /// If not set, defaults to "{FromName} confirmation code".
    /// </summary>
    public string? SubjectTemplate { get; set; }
}
