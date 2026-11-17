// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Otp.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using netDumbster.smtp;
using EmailAddress = Duende.UserManagement.EmailAddress;

namespace Duende.Platform.UserManagement;

public sealed class SmtpServerFixture : IDisposable
{
    public SimpleSmtpServer Server { get; } = SimpleSmtpServer.Start();

    public void Dispose() => Server.Stop();
}

public class SmtpOtpDispatcherIntegrationTests(SmtpServerFixture fixture) : IClassFixture<SmtpServerFixture>
{
    private readonly SimpleSmtpServer _smtpServer = fixture.Server;

    [Theory]
    [InlineData("user@example.com", "us***@ex***.com")]
    [InlineData("someone@duendesoftware.com", "so***@du***.com")]
    [InlineData("someone@gmail.com", "so***@gm***.com")]
    [InlineData("u@example.com", "***@ex***.com")]
    [InlineData("ab@xy.io", "a***@x***.io")]
    [InlineData("u@a.com", "***@***.com")]
    [InlineData("user@mail.personal.co.uk", "us***@ma***.pe***.c***.uk")]
    [InlineData("user@localhost", "us***@lo***")]
    public async Task Dispatch_with_valid_address_dispatches_email_successfully(string recipient, string maskedRecipient)
    {
        _smtpServer.ClearReceivedEmail();
        var options = new SmtpOtpDispatcherOptions
        {
            Host = "127.0.0.1",
            Port = _smtpServer.Configuration.Port,
            FromEmail = "test@example.com",
            FromName = "Test Service",
            EnableSsl = false,
            Domain = "example.com"
        };
        var logger = new FakeLogger<SmtpOtpDispatcher>();
        var dispatcher = new SmtpOtpDispatcher(Options.Create(options), new EmailContentFactory(Options.Create(options)), logger);
        var emailAddress = EmailAddress.Create(recipient);
        var address = new OtpAddress(OtpChannel.Email, emailAddress);
        PlainTextOtp.TryCreate("123456", out var otp).ShouldBeTrue();

        await dispatcher.DispatchAsync(address, otp!.Value, TimeSpan.FromMinutes(5), Ct.None);

        _smtpServer.ReceivedEmailCount.ShouldBe(1);
        var message = _smtpServer.ReceivedEmail[0];
        message.FromAddress.Address.ShouldBe("test@example.com");
        message.ToAddresses[0].Address.ShouldBe(recipient);
        message.Headers["Subject"].ShouldBe("Test Service confirmation code");
        var body = message.MessageParts[0].BodyData;
        ShouldlyExtensions.ShouldContain(body, "123-456");
        ShouldlyExtensions.ShouldContain(body, "IMPORTANT SECURITY");

        var log = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        log.Level.ShouldBe(LogLevel.Information);
        log.Message.ShouldBe($"Email sent to {maskedRecipient}.");
        var state = log.StructuredState.ShouldNotBeNull();
        state.ShouldContain(pair => pair.Key == "To" && pair.Value == maskedRecipient);
        state.ShouldNotContain(pair => pair.Value == recipient);
        log.Exception.ShouldBeNull();
    }

    [Theory]
    [InlineData("user@example.com", "us***@ex***.com")]
    [InlineData("someone@duendesoftware.com", "so***@du***.com")]
    [InlineData("someone@gmail.com", "so***@gm***.com")]
    [InlineData("u@example.com", "***@ex***.com")]
    [InlineData("ab@xy.io", "a***@x***.io")]
    [InlineData("u@a.b", "***@***.b")]
    [InlineData("user@mail.personal.co.uk", "us***@ma***.pe***.c***.uk")]
    [InlineData("user@localhost", "us***@lo***")]
    [InlineData("\"user@private\"@example.com", "us***@ex***.com")]
    public async Task Dispatch_failure_logs_masked_recipient_and_rethrows(string recipient, string maskedRecipient)
    {
        // Reserve a local port without listening so the SMTP connection is refused.
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var options = new SmtpOtpDispatcherOptions
        {
            Host = "127.0.0.1",
            Port = ((IPEndPoint)socket.LocalEndPoint!).Port,
            FromEmail = "test@example.com",
            FromName = "Test Service",
            EnableSsl = false,
            Domain = "example.com"
        };
        var logger = new FakeLogger<SmtpOtpDispatcher>();
        var dispatcher = new SmtpOtpDispatcher(Options.Create(options), new EmailContentFactory(Options.Create(options)), logger);
        var address = new OtpAddress(OtpChannel.Email, EmailAddress.Create(recipient));
        PlainTextOtp.TryCreate("123456", out var otp).ShouldBeTrue();

        var exception = await Should.ThrowAsync<SmtpException>(() =>
            dispatcher.DispatchAsync(address, otp!.Value, TimeSpan.FromMinutes(5), Ct.None));

        var log = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        log.Level.ShouldBe(LogLevel.Error);
        log.Message.ShouldBe($"Failed to send email to {maskedRecipient}.");
        var state = log.StructuredState.ShouldNotBeNull();
        state.ShouldContain(pair => pair.Key == "To" && pair.Value == maskedRecipient);
        state.ShouldNotContain(pair => pair.Value == recipient);
        log.Exception.ShouldBeSameAs(exception);
    }

    [Fact]
    public async Task Dispatch_with_html_template_dispatches_html_email()
    {
        _smtpServer.ClearReceivedEmail();
        var options = new SmtpOtpDispatcherOptions
        {
            Host = "127.0.0.1",
            Port = _smtpServer.Configuration.Port,
            FromEmail = "test@example.com",
            FromName = "Test Service",
            EnableSsl = false,
            HtmlTemplate = "<html><body><h1>{Code}</h1><p>Expires: {ExpiresMinutes}min</p></body></html>"
        };
        var dispatcher = new SmtpOtpDispatcher(Options.Create(options), new EmailContentFactory(Options.Create(options)), NullLogger<SmtpOtpDispatcher>.Instance);
        var emailAddress = EmailAddress.Create("user@example.com");
        var address = new OtpAddress(OtpChannel.Email, emailAddress);
        PlainTextOtp.TryCreate("654321", out var otp).ShouldBeTrue();

        await dispatcher.DispatchAsync(address, otp!.Value, TimeSpan.FromMinutes(10), Ct.None);

        _smtpServer.ReceivedEmailCount.ShouldBe(1);
        var message = _smtpServer.ReceivedEmail[0];
        var body = message.MessageParts[0].BodyData;
        ShouldlyExtensions.ShouldContain(body, "<h1>654-321</h1>");
        ShouldlyExtensions.ShouldContain(body, "Expires: 10min");
    }

    [Fact]
    public async Task Dispatch_with_custom_subject_uses_custom_subject()
    {
        _smtpServer.ClearReceivedEmail();
        var options = new SmtpOtpDispatcherOptions
        {
            Host = "127.0.0.1",
            Port = _smtpServer.Configuration.Port,
            FromEmail = "test@example.com",
            FromName = "Test Service",
            EnableSsl = false,
            SubjectTemplate = "Your code is {Code}"
        };
        var dispatcher = new SmtpOtpDispatcher(Options.Create(options), new EmailContentFactory(Options.Create(options)), NullLogger<SmtpOtpDispatcher>.Instance);
        var emailAddress = EmailAddress.Create("user@example.com");
        var address = new OtpAddress(OtpChannel.Email, emailAddress);
        PlainTextOtp.TryCreate("999888", out var otp).ShouldBeTrue();

        await dispatcher.DispatchAsync(address, otp!.Value, TimeSpan.FromMinutes(5), Ct.None);

        _smtpServer.ReceivedEmailCount.ShouldBe(1);
        _smtpServer.ReceivedEmail[0].Headers["Subject"].ShouldBe("Your code is 999-888");
    }
}
