// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Otp;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public class UseOtpDispatcherTests
{
    [Fact]
    public void UseOtpDispatcherGeneric_registers_implementation()
    {
        var services = new ServiceCollection();
        var builder = new TestUserAuthenticationBuilder(services);

        _ = builder.UseOtpDispatcher<TestEmailDispatcher>();

        var provider = services.BuildServiceProvider();
        var dispatchers = provider.GetServices<IOtpDispatcher>().ToList();

        dispatchers.ShouldContain(s => s is TestEmailDispatcher);
    }

    [Fact]
    public void Multiple_OtpDispatchers_can_be_registered()
    {
        var services = new ServiceCollection();
        var builder = new TestUserAuthenticationBuilder(services);

        _ = builder.UseOtpDispatcher<TestSmsDispatcher>();
        _ = builder.UseOtpDispatcher<TestEmailDispatcher>();


        var provider = services.BuildServiceProvider();
        var dispatchers = provider.GetServices<IOtpDispatcher>().ToList();

        dispatchers.Count.ShouldBe(2);

        var smsAddress = new OtpAddress(OtpChannel.Sms, PhoneNumber.Create("+1234567890"));
        var emailAddress = new OtpAddress(OtpChannel.Email, EmailAddress.Create("test@example.com"));

        // First dispatcher handles SMS
        dispatchers[0].CanDispatch(smsAddress).ShouldBeTrue();
        dispatchers[0].CanDispatch(emailAddress).ShouldBeFalse();

        // Second dispatcher handles Email
        dispatchers[1].CanDispatch(smsAddress).ShouldBeFalse();
        dispatchers[1].CanDispatch(emailAddress).ShouldBeTrue();
    }

    private sealed class TestUserAuthenticationBuilder(IServiceCollection services) : IUserAuthenticationBuilder
    {
        public IServiceCollection Services { get; } = services;
    }

    private sealed class TestEmailDispatcher : IOtpDispatcher
    {
        public bool CanDispatch(OtpAddress address) => address.Channel == OtpChannel.Email;

        public Task DispatchAsync(OtpAddress address, PlainTextOtp otp, TimeSpan expiresAfter, Ct ct) => Task.CompletedTask;
    }

    private sealed class TestSmsDispatcher : IOtpDispatcher
    {
        public bool CanDispatch(OtpAddress address) => address.Channel == OtpChannel.Sms;

        public Task DispatchAsync(OtpAddress address, PlainTextOtp otp, TimeSpan expiresAfter, Ct ct) => Task.CompletedTask;
    }
}
