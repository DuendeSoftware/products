// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace AspNetIdentityApp.Services;

public sealed class OtpService(IOptions<SmtpOptions> smtpOptions)
{
    private static readonly TimeSpan OtpExpiry = TimeSpan.FromMinutes(10);

    public string GenerateOtp() => RandomNumberGenerator.GetInt32(100000, 999999).ToString("D6", CultureInfo.InvariantCulture);

    public async Task SendOtpAsync(string toEmail, string otp, Ct ct)
    {
        var opts = smtpOptions.Value;
        using var client = new SmtpClient(opts.Host, opts.Port)
        {
            EnableSsl = opts.EnableSsl,
            Credentials = CredentialCache.DefaultNetworkCredentials
        };

        var message = new MailMessage
        {
            From = new MailAddress(opts.FromEmail, opts.FromName),
            Subject = "Your one-time password",
            Body = $"""
                Your one-time password is: {otp}

                This code expires in {(int)OtpExpiry.TotalMinutes} minutes.

                If you did not request this code, please ignore this email.
                """,
            IsBodyHtml = false
        };
        message.To.Add(toEmail);

        await client.SendMailAsync(message, ct);
    }
}
