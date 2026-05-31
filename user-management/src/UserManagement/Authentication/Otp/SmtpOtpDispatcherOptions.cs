// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace Duende.UserManagement.Authentication.Otp;

/// <summary>
/// Configuration options for the SMTP-based OTP email dispatcher.
/// </summary>
public sealed class SmtpOtpDispatcherOptions
{
    /// <summary>Gets or sets the SMTP server hostname.</summary>
    [Required]
    public string Host { get; set; } = null!;

    /// <summary>Gets or sets the SMTP server port. Defaults to 1025.</summary>
    public int Port { get; set; } = 1025;

    /// <summary>Gets or sets a value indicating whether SSL is enabled. Defaults to <c>true</c>.</summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>Gets or sets the sender email address.</summary>
    [Required]
    public string FromEmail { get; set; } = null!;

    /// <summary>Gets or sets the sender display name.</summary>
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
