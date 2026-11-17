// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Otp;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class OtpHowToTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IOtpSender _otpSender = null!;
    private IOtpAuthenticator _otpAuthenticator = null!;
    private IUserAuthenticatorsSelfService _selfService = null!;
    private FakeOtpDispatcher _otpDispatcher = null!;
    private FakeTimeProvider _timeProvider = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _otpSender = _serviceProvider.GetRequiredService<IOtpSender>();
        _otpAuthenticator = _serviceProvider.GetRequiredService<IOtpAuthenticator>();
        _selfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _otpDispatcher = _serviceProvider.GetRequiredService<FakeOtpDispatcher>();
        _timeProvider = _serviceProvider.GetRequiredService<FakeTimeProvider>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static async Task<SendOtpResult> SendOtpAsync(IOtpSender sender, OtpAddress address, Ct ct)
    {
        // begin-snippet: otp-send
        var result = await sender.TrySendOtpAsync(address, ct);
        // end-snippet
        return result;
    }

    private static async Task<OtpAuthenticationResult> VerifyOtpAsync(
        IOtpAuthenticator authenticator, PlainTextOtp otp, OtpToken token, Ct ct)
    {
        // begin-snippet: otp-verify
        var result = await authenticator.TryAuthenticateAsync(otp, token, ct);
        // end-snippet
        return result;
    }

    private static async Task<bool> AddOtpAddressAsync(
        IUserAuthenticatorsSelfService selfService, UserSubjectId subjectId, PlainTextOtp otp, OtpToken token, Ct ct)
    {
        // begin-snippet: otp-add-address-self-service
        var added = await selfService.TryAddOtpAddressAsync(subjectId, otp, token, ct);
        // end-snippet
        return added;
    }

    private static async Task<UserSubjectId?> RegisterWithOtpAsync(
        IOtpAuthenticator authenticator, PlainTextOtp otp, OtpToken token, Ct ct)
    {
        // begin-snippet: otp-register-user
        var result = await authenticator.TryAuthenticateAsync(otp, token, ct);
        var subjectId = result switch
        {
            OtpAuthenticationResult.Success success => success.UserSubjectId,
            _ => (UserSubjectId?)null
        };
        // end-snippet
        return subjectId;
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Sending_an_otp_dispatches_a_code_to_the_address()
    {
        var address = TestData.CreateOtpAddress();

        var result = (await SendOtpAsync(_otpSender, address, _ct)).ShouldBeOfType<SendOtpResult.Sent>();

        result.Token.ShouldNotBe(default);
        _otpDispatcher.Calls.ShouldHaveSingleItem().Address.ShouldBe(address);
    }

    [Fact]
    public async Task A_sent_otp_verifies_once_and_fails_on_reuse()
    {
        var address = TestData.CreateOtpAddress();
        var sendResult = (await SendOtpAsync(_otpSender, address, _ct)).ShouldBeOfType<SendOtpResult.Sent>();
        var otp = _otpDispatcher.Calls.ShouldHaveSingleItem().Otp;

        var firstVerify = await VerifyOtpAsync(_otpAuthenticator, otp, sendResult.Token, _ct);

        firstVerify.ShouldBeOfType<OtpAuthenticationResult.Success>().Address.ShouldBe(address);

        var secondVerify = await VerifyOtpAsync(_otpAuthenticator, otp, sendResult.Token, _ct);

        _ = secondVerify.ShouldBeOfType<OtpAuthenticationResult.Failure>();
    }

    [Fact]
    public async Task A_sent_otp_fails_to_verify_after_it_expires()
    {
        var address = TestData.CreateOtpAddress();
        var sendResult = (await SendOtpAsync(_otpSender, address, _ct)).ShouldBeOfType<SendOtpResult.Sent>();
        var otp = _otpDispatcher.Calls.ShouldHaveSingleItem().Otp;
        _timeProvider.Advance(sendResult.ExpiresAfter);

        var result = await VerifyOtpAsync(_otpAuthenticator, otp, sendResult.Token, _ct);

        _ = result.ShouldBeOfType<OtpAuthenticationResult.Failure>();
    }

    [Fact]
    public async Task Verifying_an_otp_for_a_new_address_registers_a_user()
    {
        var address = TestData.CreateOtpAddress();
        var sendResult = (await SendOtpAsync(_otpSender, address, _ct)).ShouldBeOfType<SendOtpResult.Sent>();
        var otp = _otpDispatcher.Calls.ShouldHaveSingleItem().Otp;

        var subjectId = await RegisterWithOtpAsync(_otpAuthenticator, otp, sendResult.Token, _ct);

        _ = subjectId.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_signed_in_user_can_add_a_verified_otp_address_as_a_second_address()
    {
        var firstAddress = TestData.CreateOtpAddress();
        var firstSend = (await SendOtpAsync(_otpSender, firstAddress, _ct)).ShouldBeOfType<SendOtpResult.Sent>();
        var firstOtp = _otpDispatcher.Calls.ShouldHaveSingleItem().Otp;
        var subjectId = (await RegisterWithOtpAsync(_otpAuthenticator, firstOtp, firstSend.Token, _ct)).ShouldNotBeNull();
        _otpDispatcher.ClearCalls();

        var secondAddress = TestData.CreateOtpAddress(typeof(PhoneNumber));
        var secondSend = (await SendOtpAsync(_otpSender, secondAddress, _ct)).ShouldBeOfType<SendOtpResult.Sent>();
        var secondOtp = _otpDispatcher.Calls.ShouldHaveSingleItem().Otp;

        var added = await AddOtpAddressAsync(_selfService, subjectId, secondOtp, secondSend.Token, _ct);

        added.ShouldBeTrue();
        var record = (await _selfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull();
        record.OtpAddresses.ShouldContain(secondAddress);
    }

    [Fact]
    public async Task Adding_an_address_with_an_already_consumed_token_is_rejected()
    {
        var firstAddress = TestData.CreateOtpAddress();
        var firstSend = (await SendOtpAsync(_otpSender, firstAddress, _ct)).ShouldBeOfType<SendOtpResult.Sent>();
        var firstOtp = _otpDispatcher.Calls.ShouldHaveSingleItem().Otp;
        var subjectId = (await RegisterWithOtpAsync(_otpAuthenticator, firstOtp, firstSend.Token, _ct)).ShouldNotBeNull();
        _otpDispatcher.ClearCalls();

        var secondAddress = TestData.CreateOtpAddress(typeof(PhoneNumber));
        var secondSend = (await SendOtpAsync(_otpSender, secondAddress, _ct)).ShouldBeOfType<SendOtpResult.Sent>();
        var secondOtp = _otpDispatcher.Calls.ShouldHaveSingleItem().Otp;

        // Consume the token by verifying it through TryAuthenticateAsync first, the way a sign-in would.
        var consumed = await VerifyOtpAsync(_otpAuthenticator, secondOtp, secondSend.Token, _ct);
        _ = consumed.ShouldBeOfType<OtpAuthenticationResult.Success>();

        // The token has already verified once, so adding the address with the same code and token fails.
        var added = await AddOtpAddressAsync(_selfService, subjectId, secondOtp, secondSend.Token, _ct);

        added.ShouldBeFalse();
    }
}
