// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Otp.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Duende.Platform.UserManagement;

public class SmtpOtpDispatcherTests
{
    private readonly SmtpOtpDispatcherOptions _defaultOptions = new()
    {
        Host = "localhost",
        Port = 1025,
        FromEmail = "test@example.com",
        FromName = "Test Service",
        EnableSsl = false,
        Domain = "example.com"
    };

    [Fact]
    public void Can_dispatch_with_email_channel_returns_true()
    {
        var dispatcher = CreateSmtpDispatcher();
        var address = new OtpAddress(OtpChannel.Email, EmailAddress.Create("user@example.com"));

        var result = dispatcher.CanDispatch(address);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Can_dispatch_with_non_email_channel_returns_false()
    {
        var dispatcher = CreateSmtpDispatcher();
        var address = new OtpAddress(OtpChannel.Sms, PhoneNumber.Create("+1234567890"));

        var result = dispatcher.CanDispatch(address);

        result.ShouldBeFalse();
    }

    [Fact]
    public void Can_dispatch_with_email_channel_but_non_email_subject_id_returns_false()
    {
        var dispatcher = CreateSmtpDispatcher();
        var address = new OtpAddress(OtpChannel.Email, PhoneNumber.Create("+1234567890"));

        var result = dispatcher.CanDispatch(address);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task Dispatch_with_invalid_channel_throws_argument_exception()
    {
        var dispatcher = CreateSmtpDispatcher();
        var address = new OtpAddress(OtpChannel.Sms, PhoneNumber.Create("+1234567890"));
        PlainTextOtp.TryCreate("123456", out var otp).ShouldBeTrue();

        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await dispatcher.DispatchAsync(address, otp!.Value, TimeSpan.FromMinutes(5), Ct.None)
        );

        exception.ParamName.ShouldBe("address");
    }

    [Fact]
    public async Task Dispatch_with_invalid_subject_id_throws_argument_exception()
    {
        var dispatcher = CreateSmtpDispatcher();
        var address = new OtpAddress(OtpChannel.Email, PhoneNumber.Create("+1234567890"));
        PlainTextOtp.TryCreate("123456", out var otp).ShouldBeTrue();

        var exception = await Should.ThrowAsync<ArgumentException>(async () =>
            await dispatcher.DispatchAsync(address, otp!.Value, TimeSpan.FromMinutes(5), Ct.None)
        );

        exception.ParamName.ShouldBe("address");
    }

    private SmtpOtpDispatcher CreateSmtpDispatcher(SmtpOtpDispatcherOptions? options = null)
    {
        var opts = Options.Create(options ?? _defaultOptions);
        var emailContentFactory = new EmailContentFactory(opts);
        return new SmtpOtpDispatcher(opts, emailContentFactory, NullLogger<SmtpOtpDispatcher>.Instance);
    }
}
