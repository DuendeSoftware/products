// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Otp.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using netDumbster.smtp;
using EmailAddress = Duende.UserManagement.EmailAddress;

namespace Duende.Platform.UserManagement;

public sealed class SmtpServerFixture : IDisposable
{
    public SimpleSmtpServer Server { get; } = SimpleSmtpServer.Start();

    public void Dispose() => Server.Stop();
}

public class SmtpOtpSenderIntegrationTests(SmtpServerFixture fixture) : IClassFixture<SmtpServerFixture>
{
    private readonly SimpleSmtpServer _smtpServer = fixture.Server;

    [Fact]
    public async Task Send_with_valid_address_sends_email_successfully()
    {
        _smtpServer.ClearReceivedEmail();
        var options = new SmtpOtpSenderOptions
        {
            Host = "127.0.0.1",
            Port = _smtpServer.Configuration.Port,
            FromEmail = "test@example.com",
            FromName = "Test Service",
            EnableSsl = false,
            Domain = "example.com"
        };
        var sender = new SmtpOtpSender(Options.Create(options), new EmailContentFactory(Options.Create(options)), NullLogger<SmtpOtpSender>.Instance);
        var emailAddress = EmailAddress.Create("user@example.com");
        var address = new OtpAddress(OtpChannel.Email, emailAddress);
        PlainTextOtp.TryCreate("123456", out var otp).ShouldBeTrue();

        await sender.SendAsync(address, otp!.Value, TimeSpan.FromMinutes(5), Ct.None);

        _smtpServer.ReceivedEmailCount.ShouldBe(1);
        var message = _smtpServer.ReceivedEmail[0];
        message.FromAddress.Address.ShouldBe("test@example.com");
        message.ToAddresses[0].Address.ShouldBe("user@example.com");
        message.Headers["Subject"].ShouldBe("Test Service confirmation code");
        var body = message.MessageParts[0].BodyData;
        ShouldlyExtensions.ShouldContain(body, "123-456");
        ShouldlyExtensions.ShouldContain(body, "IMPORTANT SECURITY");
    }

    [Fact]
    public async Task Send_with_html_template_sends_html_email()
    {
        _smtpServer.ClearReceivedEmail();
        var options = new SmtpOtpSenderOptions
        {
            Host = "127.0.0.1",
            Port = _smtpServer.Configuration.Port,
            FromEmail = "test@example.com",
            FromName = "Test Service",
            EnableSsl = false,
            HtmlTemplate = "<html><body><h1>{Code}</h1><p>Expires: {ExpiresMinutes}min</p></body></html>"
        };
        var sender = new SmtpOtpSender(Options.Create(options), new EmailContentFactory(Options.Create(options)), NullLogger<SmtpOtpSender>.Instance);
        var emailAddress = EmailAddress.Create("user@example.com");
        var address = new OtpAddress(OtpChannel.Email, emailAddress);
        PlainTextOtp.TryCreate("654321", out var otp).ShouldBeTrue();

        await sender.SendAsync(address, otp!.Value, TimeSpan.FromMinutes(10), Ct.None);

        _smtpServer.ReceivedEmailCount.ShouldBe(1);
        var message = _smtpServer.ReceivedEmail[0];
        var body = message.MessageParts[0].BodyData;
        ShouldlyExtensions.ShouldContain(body, "<h1>654-321</h1>");
        ShouldlyExtensions.ShouldContain(body, "Expires: 10min");
    }

    [Fact]
    public async Task Send_with_custom_subject_uses_custom_subject()
    {
        _smtpServer.ClearReceivedEmail();
        var options = new SmtpOtpSenderOptions
        {
            Host = "127.0.0.1",
            Port = _smtpServer.Configuration.Port,
            FromEmail = "test@example.com",
            FromName = "Test Service",
            EnableSsl = false,
            SubjectTemplate = "Your code is {Code}"
        };
        var sender = new SmtpOtpSender(Options.Create(options), new EmailContentFactory(Options.Create(options)), NullLogger<SmtpOtpSender>.Instance);
        var emailAddress = EmailAddress.Create("user@example.com");
        var address = new OtpAddress(OtpChannel.Email, emailAddress);
        PlainTextOtp.TryCreate("999888", out var otp).ShouldBeTrue();

        await sender.SendAsync(address, otp!.Value, TimeSpan.FromMinutes(5), Ct.None);

        _smtpServer.ReceivedEmailCount.ShouldBe(1);
        _smtpServer.ReceivedEmail[0].Headers["Subject"].ShouldBe("Your code is 999-888");
    }
}
