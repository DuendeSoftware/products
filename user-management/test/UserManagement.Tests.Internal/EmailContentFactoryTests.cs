// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Otp.Internal;
using Microsoft.Extensions.Options;

namespace Duende.Platform.UserManagement;

public class EmailContentFactoryTests
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
    public async Task Create_returns_EmailContent_with_correct_properties()
    {
        var factory = CreateFactory();
        PlainTextOtp.TryCreate("12345678", out var otp).ShouldBeTrue();

        var result = await factory.CreateAsync(otp!.Value, TimeSpan.FromMinutes(5), Ct.None);

        result.Subject.ShouldBe("Test Service confirmation code");
        result.IsBodyHtml.ShouldBeFalse();
        result.Body.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Create_formats_otp_with_hyphens()
    {
        var factory = CreateFactory();
        PlainTextOtp.TryCreate("12345678", out var otp).ShouldBeTrue();

        var result = await factory.CreateAsync(otp!.Value, TimeSpan.FromMinutes(5), Ct.None);

        ShouldlyExtensions.ShouldContain(result.Body, "1234-5678");
    }

    [Fact]
    public async Task Create_includes_expiration_time()
    {
        var factory = CreateFactory();
        PlainTextOtp.TryCreate("12345678", out var otp).ShouldBeTrue();

        var result = await factory.CreateAsync(otp!.Value, TimeSpan.FromMinutes(10), Ct.None);

        ShouldlyExtensions.ShouldContain(result.Body, "expires after 10 minute(s)");
    }

    [Fact]
    public async Task Create_includes_security_warnings()
    {
        var factory = CreateFactory();
        PlainTextOtp.TryCreate("12345678", out var otp).ShouldBeTrue();

        var result = await factory.CreateAsync(otp!.Value, TimeSpan.FromMinutes(5), Ct.None);

        ShouldlyExtensions.ShouldContain(result.Body, "IMPORTANT SECURITY INFORMATION");
        ShouldlyExtensions.ShouldContain(result.Body, "You should only use this code if you requested it");
        ShouldlyExtensions.ShouldContain(result.Body, "If you did not request this code, please ignore this email");
        ShouldlyExtensions.ShouldContain(result.Body, "Only enter this code on example.com");
        ShouldlyExtensions.ShouldContain(result.Body, "Do not share this code with anyone");
        ShouldlyExtensions.ShouldContain(result.Body, "Test Service will never ask you for this code");
    }

    [Fact]
    public async Task Create_without_domain_uses_generic_message()
    {
        var options = new SmtpOtpDispatcherOptions
        {
            Host = "localhost",
            Port = 1025,
            FromEmail = "test@example.com",
            FromName = "My App",
            EnableSsl = false
        };
        var factory = CreateFactory(options);
        PlainTextOtp.TryCreate("12345678", out var otp).ShouldBeTrue();

        var result = await factory.CreateAsync(otp!.Value, TimeSpan.FromMinutes(5), Ct.None);

        ShouldlyExtensions.ShouldContain(result.Body, "Only enter this code on our official website");
        ShouldlyExtensions.ShouldContain(result.Body, "My App will never ask you for this code");
    }

    [Fact]
    public async Task Create_with_html_template_returns_html()
    {
        var options = new SmtpOtpDispatcherOptions
        {
            Host = "localhost",
            Port = 1025,
            FromEmail = "test@example.com",
            FromName = "Test Service",
            EnableSsl = false,
            HtmlTemplate = "<html><body><h1>{Code}</h1><p>Expires in {ExpiresMinutes} minutes</p></body></html>"
        };
        var factory = CreateFactory(options);
        PlainTextOtp.TryCreate("12345678", out var otp).ShouldBeTrue();

        var result = await factory.CreateAsync(otp!.Value, TimeSpan.FromMinutes(5), Ct.None);

        ShouldlyExtensions.ShouldContain(result.Body, "<html>");
        ShouldlyExtensions.ShouldContain(result.Body, "<h1>1234-5678</h1>");
        ShouldlyExtensions.ShouldContain(result.Body, "Expires in 5 minutes");
        result.IsBodyHtml.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_with_plain_text_template_returns_plain_text()
    {
        var options = new SmtpOtpDispatcherOptions
        {
            Host = "localhost",
            Port = 1025,
            FromEmail = "test@example.com",
            FromName = "Test Service",
            EnableSsl = false,
            PlainTextTemplate = "Your code is {Code}. Valid for {ExpiresMinutes} minutes."
        };
        var factory = CreateFactory(options);
        PlainTextOtp.TryCreate("12345678", out var otp).ShouldBeTrue();

        var result = await factory.CreateAsync(otp!.Value, TimeSpan.FromMinutes(10), Ct.None);

        result.Body.ShouldBe("Your code is 1234-5678. Valid for 10 minutes.");
        result.IsBodyHtml.ShouldBeFalse();
    }

    [Fact]
    public async Task Create_with_subject_template_uses_custom_subject()
    {
        var options = new SmtpOtpDispatcherOptions
        {
            Host = "localhost",
            Port = 1025,
            FromEmail = "test@example.com",
            FromName = "Test Service",
            EnableSsl = false,
            SubjectTemplate = "Code {Code} from {FromName}"
        };
        var factory = CreateFactory(options);
        PlainTextOtp.TryCreate("12345678", out var otp).ShouldBeTrue();

        var result = await factory.CreateAsync(otp!.Value, TimeSpan.FromMinutes(5), Ct.None);

        result.Subject.ShouldBe("Code 1234-5678 from Test Service");
    }

    [Fact]
    public async Task Create_template_replaces_all_placeholders()
    {
        var options = new SmtpOtpDispatcherOptions
        {
            Host = "localhost",
            Port = 1025,
            FromEmail = "test@example.com",
            FromName = "Test Service",
            EnableSsl = false,
            Domain = "example.com",
            SubjectTemplate = "Your {FromName} code: {Code} (expires in {ExpiresMinutes} min on {Domain})"
        };
        var factory = CreateFactory(options);
        PlainTextOtp.TryCreate("12345678", out var otp).ShouldBeTrue();

        var result = await factory.CreateAsync(otp!.Value, TimeSpan.FromMinutes(10), Ct.None);

        result.Subject.ShouldBe("Your Test Service code: 1234-5678 (expires in 10 min on example.com)");
    }

    [Fact]
    public async Task Create_template_without_domain_uses_default_text()
    {
        var options = new SmtpOtpDispatcherOptions
        {
            Host = "localhost",
            Port = 1025,
            FromEmail = "test@example.com",
            FromName = "Test Service",
            EnableSsl = false,
            PlainTextTemplate = "Code: {Code} on {Domain}"
        };
        var factory = CreateFactory(options);
        PlainTextOtp.TryCreate("12345678", out var otp).ShouldBeTrue();

        var result = await factory.CreateAsync(otp!.Value, TimeSpan.FromMinutes(5), Ct.None);

        result.Body.ShouldBe("Code: 1234-5678 on our official website");
    }

    private EmailContentFactory CreateFactory(SmtpOtpDispatcherOptions? options = null)
    {
        var opts = Options.Create(options ?? _defaultOptions);
        return new EmailContentFactory(opts);
    }
}

