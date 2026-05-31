// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Otp.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class SmtpOtpDispatcher(
    IOptions<SmtpOtpDispatcherOptions> options,
    IEmailContentFactory emailContentFactory,
    ILogger<SmtpOtpDispatcher> logger) : IOtpDispatcher
{
    private readonly SmtpOtpDispatcherOptions _options = options.Value;

    public bool CanDispatch(OtpAddress address) => address.Channel == OtpChannel.Email && address.SubjectId is EmailAddress;

    public async Task DispatchAsync(OtpAddress address, PlainTextOtp otp, TimeSpan expiresAfter, Ct ct)
    {
        if (address.Channel != OtpChannel.Email)
        {
            throw new ArgumentException($"Invalid {nameof(address.Channel)}", nameof(address));
        }

        if (address.SubjectId is not EmailAddress emailAddress)
        {
            throw new ArgumentException($"Invalid {nameof(address.SubjectId)}", nameof(address));
        }

        var from = new MailAddress(_options.FromEmail, _options.FromName);
        var to = new MailAddress(emailAddress.ToString());

        var (subject, body, isBodyHtml) = await emailContentFactory.CreateAsync(otp, expiresAfter, ct);

        using var client = new SmtpClient(_options.Host, _options.Port);
        client.EnableSsl = _options.EnableSsl;
        client.UseDefaultCredentials = false;

        using var message = new MailMessage();
        message.From = from;
        message.To.Add(to);
        message.Subject = subject;
        message.Body = body;
        message.IsBodyHtml = isBodyHtml;

        try
        {
            await client.SendMailAsync(message, ct);
        }
        catch (Exception ex)
        {
            logger.FailedToSendEmail(LogLevel.Error, ex, emailAddress);
            throw;
        }

        logger.EmailSent(LogLevel.Information, emailAddress);
    }
}
