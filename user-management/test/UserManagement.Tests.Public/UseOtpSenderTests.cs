// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Otp;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public class UseOtpSenderTests
{
    [Fact]
    public void UseOtpSenderGeneric_registers_implementation()
    {
        var services = new ServiceCollection();
        var builder = new TestUserAuthenticationBuilder(services);

        _ = builder.UseOtpSender<TestEmailSender>();

        var provider = services.BuildServiceProvider();
        var senders = provider.GetServices<IOtpSender>().ToList();

        senders.ShouldContain(s => s is TestEmailSender);
    }

    [Fact]
    public void Multiple_OtpSenders_can_be_registered()
    {
        var services = new ServiceCollection();
        var builder = new TestUserAuthenticationBuilder(services);

        _ = builder.UseOtpSender<TestSmsSender>();
        _ = builder.UseOtpSender<TestEmailSender>();


        var provider = services.BuildServiceProvider();
        var senders = provider.GetServices<IOtpSender>().ToList();

        senders.Count.ShouldBe(2);

        var smsAddress = new OtpAddress(OtpChannel.Sms, PhoneNumber.Create("+1234567890"));
        var emailAddress = new OtpAddress(OtpChannel.Email, EmailAddress.Create("test@example.com"));

        // First sender handles SMS
        senders[0].CanSend(smsAddress).ShouldBeTrue();
        senders[0].CanSend(emailAddress).ShouldBeFalse();

        // Second sender handles Email
        senders[1].CanSend(smsAddress).ShouldBeFalse();
        senders[1].CanSend(emailAddress).ShouldBeTrue();
    }

    private sealed class TestUserAuthenticationBuilder(IServiceCollection services) : IUserAuthenticationBuilder
    {
        public IServiceCollection Services { get; } = services;
    }

    private sealed class TestEmailSender : IOtpSender
    {
        public bool CanSend(OtpAddress address) => address.Channel == OtpChannel.Email;

        public Task SendAsync(OtpAddress address, PlainTextOtp otp, TimeSpan expiresAfter, Ct ct) => Task.CompletedTask;
    }

    private sealed class TestSmsSender : IOtpSender
    {
        public bool CanSend(OtpAddress address) => address.Channel == OtpChannel.Sms;

        public Task SendAsync(OtpAddress address, PlainTextOtp otp, TimeSpan expiresAfter, Ct ct) => Task.CompletedTask;
    }
}
