// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Otp.Internal;

internal sealed class EmailContentFactory(IOptions<SmtpOtpDispatcherOptions> options) : IEmailContentFactory
{
    private readonly SmtpOtpDispatcherOptions _options = options.Value;

    public Task<EmailContent> CreateAsync(PlainTextOtp otp, TimeSpan expiresAfter, Ct ct)
    {
        var otpText = string.Join('-', otp.ToTextGroups());

        string subject;
        string body;
        bool isHtml;

        // Use custom templates if configured, otherwise use defaults
        if (!string.IsNullOrWhiteSpace(_options.SubjectTemplate))
        {
            subject = FormatTemplate(_options.SubjectTemplate, otpText, expiresAfter);
        }
        else
        {
            subject = $"{_options.FromName} confirmation code";
        }

        if (!string.IsNullOrWhiteSpace(_options.HtmlTemplate))
        {
            body = FormatTemplate(_options.HtmlTemplate, otpText, expiresAfter);
            isHtml = true;
        }
        else if (!string.IsNullOrWhiteSpace(_options.PlainTextTemplate))
        {
            body = FormatTemplate(_options.PlainTextTemplate, otpText, expiresAfter);
            isHtml = false;
        }
        else
        {
            body = BuildDefaultBody(otpText, expiresAfter);
            isHtml = false;
        }

        return Task.FromResult(new EmailContent(subject, body, isHtml));
    }

    private string BuildDefaultBody(string otpText, TimeSpan expiresAfter)
    {
        var body = $"{otpText} is your {_options.FromName} confirmation code (expires after {expiresAfter.TotalMinutes:F0} minute(s))\n\n";
        body += "IMPORTANT SECURITY INFORMATION:\n";
        body += "- You should only use this code if you requested it\n";
        body += "- If you did not request this code, please ignore this email\n";

        if (!string.IsNullOrWhiteSpace(_options.Domain))
        {
            body += $"- Only enter this code on {_options.Domain}\n";
        }
        else
        {
            body += "- Only enter this code on our official website\n";
        }

        body += "- Do not share this code with anyone\n";
        body += $"- {_options.FromName} will never ask you for this code";

        return body;
    }

    private string FormatTemplate(string template, string otpText, TimeSpan expiresAfter) => template
        .Replace("{Code}", otpText, StringComparison.Ordinal)
        .Replace("{FromName}", _options.FromName, StringComparison.Ordinal)
        .Replace("{ExpiresMinutes}", expiresAfter.TotalMinutes.ToString("F0", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{Domain}", _options.Domain ?? "our official website", StringComparison.Ordinal);
}
