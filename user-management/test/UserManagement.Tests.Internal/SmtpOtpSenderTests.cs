// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Otp.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Duende.Platform.UserManagement;

public class SmtpOtpSenderTests
{
    private readonly SmtpOtpSenderOptions _defaultOptions = new()
    {
        Host = "localhost",
        Port = 1025,
        FromEmail = "test@example.com",
        FromName = "Test Service",
        EnableSsl = false,
        Domain = "example.com"
    };

    [Fact]
    public void Can_send_with_email_channel_returns_true()
    {
        var sender = CreateSmtpSender();
        var address = new OtpAddress(OtpChannel.Email, EmailAddress.Create("user@example.com"));

        var result = sender.CanSend(address);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Can_send_with_non_email_channel_returns_false()
    {
        var sender = CreateSmtpSender();
        var address = new OtpAddress(OtpChannel.Sms, PhoneNumber.Create("+1234567890"));

        var result = sender.CanSend(address);

        result.ShouldBeFalse();
    }

    [Fact]
    public void Can_send_with_email_channel_but_non_email_subject_id_returns_false()
    {
        var sender = CreateSmtpSender();
        var address = new OtpAddress(OtpChannel.Email, PhoneNumber.Create("+1234567890"));

        var result = sender.CanSend(address);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task Send_with_invalid_channel_throws_argument_exception()
    {
        var sender = CreateSmtpSender();
        var address = new OtpAddress(OtpChannel.Sms, PhoneNumber.Create("+1234567890"));
        PlainTextOtp.TryCreate("123456", out var otp).ShouldBeTrue();

        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await sender.SendAsync(address, otp!.Value, TimeSpan.FromMinutes(5), Ct.None)
        );

        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public async Task Send_with_invalid_subject_id_throws_argument_exception()
    {
        var sender = CreateSmtpSender();
        var address = new OtpAddress(OtpChannel.Email, PhoneNumber.Create("+1234567890"));
        PlainTextOtp.TryCreate("123456", out var otp).ShouldBeTrue();

        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await sender.SendAsync(address, otp!.Value, TimeSpan.FromMinutes(5), Ct.None)
        );

        exception.ParamName.ShouldBe("address");
    }

    private SmtpOtpSender CreateSmtpSender(SmtpOtpSenderOptions? options = null)
    {
        var opts = Options.Create(options ?? _defaultOptions);
        var emailContentFactory = new EmailContentFactory(opts);
        return new SmtpOtpSender(opts, emailContentFactory, NullLogger<SmtpOtpSender>.Instance);
    }
}
