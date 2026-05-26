// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Otp;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

[Trait("PasswordHashing", "True")]
public sealed class OtpAuthentication : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private readonly PlainTextOtp _incorrectOtp = PlainTextOtp.Create("123456");
    private IOtpAuthenticator _authenticator = null!;
    private FakeOtpSender _otpSender = null!;
    private ServiceProvider _serviceProvider = null!;
    private FakeTimeProvider _timeProvider = null!;

    public static TheoryData<Type> SubjectIdTypes { get; } = [.. TestData.SubjectIdTypes];

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _authenticator = _serviceProvider.GetRequiredService<IOtpAuthenticator>();
        _otpSender = _serviceProvider.GetRequiredService<FakeOtpSender>();
        _timeProvider = _serviceProvider.GetRequiredService<FakeTimeProvider>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Theory]
    [MemberData(nameof(SubjectIdTypes))]
    public async Task CanSendOtp(Type subjectIdType)
    {
        // arrange
        var address = TestData.CreateOtpAddress(subjectIdType);

        // act
        var result = await _authenticator.TrySendOtpAsync(address, _ct);

        // assert
        _ = result.ShouldNotBeNull();
        result.Sent.ShouldBeTrue();
        _ = result.Token.ShouldNotBeNull();
        result.ExpiresAfter.ShouldBe(TimeSpan.FromMinutes(5));
        result.ExpiresAtUtc.ShouldBe(_timeProvider.GetUtcNow() + TimeSpan.FromMinutes(5));
        result.SendingBlockedFor.ShouldBe(TimeSpan.FromMinutes(1));
        result.SendingBlockedUntilUtc.ShouldBe(_timeProvider.GetUtcNow() + TimeSpan.FromMinutes(1));

        var (sentAddress, otp, expiresAfter) = _otpSender.Calls.ShouldHaveSingleItem();
        sentAddress.ShouldBe(address);

        var otpGroups = otp.ToTextGroups();
        otpGroups.Count.ShouldBe(2);
        foreach (var group in otpGroups)
        {
            group.Length.ShouldBe(4);
            group.ShouldAllBe(c => char.IsAsciiLetterOrDigit(c));
            group.ShouldNotContain(c => char.IsAsciiLetterLower(c));
        }

        otp.Text.ShouldBe(string.Join("", otpGroups));
        expiresAfter.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Theory]
    [MemberData(nameof(SubjectIdTypes))]
    public async Task CanAuthenticate(Type subjectIdType)
    {
        var address = TestData.CreateOtpAddress(subjectIdType);
        var sendResult = (await _authenticator.TrySendOtpAsync(address, _ct)).ShouldNotBeNull();
        sendResult.Sent.ShouldBeTrue();
        _otpSender.Calls.ShouldNotBeEmpty();
        var otp = _otpSender.Calls.First().Otp;

        var result = await _authenticator.TryAuthenticateAsync(otp, sendResult.Token.Value, _ct);

        var success = result.ShouldBeOfType<OtpAuthenticationResult.Success>();
        success.Address.ShouldBe(address);
    }

    [Theory]
    [MemberData(nameof(SubjectIdTypes))]
    public async Task CanAuthenticateAgain(Type subjectIdType)
    {
        // arrange
        var address = TestData.CreateOtpAddress(subjectIdType);

        var firstSendResult = (await _authenticator.TrySendOtpAsync(address, _ct)).ShouldNotBeNull();
        firstSendResult.Sent.ShouldBeTrue();
        _otpSender.Calls.ShouldNotBeEmpty();
        var firstOtp = _otpSender.Calls.First().Otp;
        var firstSuccess = (await _authenticator.TryAuthenticateAsync(firstOtp, firstSendResult.Token.Value, _ct)).ShouldBeOfType<OtpAuthenticationResult.Success>();
        _otpSender.ClearCalls();

        var secondSendResult = (await _authenticator.TrySendOtpAsync(address, _ct)).ShouldNotBeNull();
        secondSendResult.Sent.ShouldBeTrue();
        _otpSender.Calls.ShouldNotBeEmpty();
        var secondOtp = _otpSender.Calls.First().Otp;

        // act
        var result = await _authenticator.TryAuthenticateAsync(secondOtp, secondSendResult.Token.Value, _ct);

        // assert
        var success = result.ShouldBeOfType<OtpAuthenticationResult.Success>();
        success.Address.ShouldBe(address);
        success.UserSubjectId.ShouldBe(firstSuccess.UserSubjectId);
    }

    [Fact]
    public async Task Cannot_send_otp_again_immediately()
    {
        var address = TestData.CreateOtpAddress();
        _ = (await _authenticator.TrySendOtpAsync(address, _ct)).ShouldNotBeNull();
        _timeProvider.Advance(TimeSpan.FromSeconds(30));

        var result = (await _authenticator.TrySendOtpAsync(address, _ct)).ShouldNotBeNull();

        result.Sent.ShouldBeFalse();
        result.SendingBlockedFor.ShouldBe(TimeSpan.FromSeconds(30));
        result.SendingBlockedUntilUtc.ShouldBe(_timeProvider.GetUtcNow() + TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Can_send_otp_again_after_waiting()
    {
        var address = TestData.CreateOtpAddress();
        var firstResult = (await _authenticator.TrySendOtpAsync(address, _ct)).ShouldNotBeNull();
        _timeProvider.Advance(firstResult.SendingBlockedFor);

        var result = (await _authenticator.TrySendOtpAsync(address, _ct)).ShouldNotBeNull();

        result.Sent.ShouldBeTrue();
    }

    [Fact]
    public async Task Cannot_authenticate_with_non_existent_token()
    {
        var token = OtpToken.Parse(Guid.CreateVersion7(_timeProvider.GetUtcNow()).ToString());

        var result = await _authenticator.TryAuthenticateAsync(_incorrectOtp, token, _ct);

        _ = result.ShouldBeOfType<OtpAuthenticationResult.Failure>();
    }

    [Fact]
    public async Task Cannot_authenticate_with_the_incorrect_otp()
    {
        var sendResult = (await _authenticator.TrySendOtpAsync(TestData.CreateOtpAddress(), _ct)).ShouldNotBeNull();
        sendResult.Sent.ShouldBeTrue();

        var result = await _authenticator.TryAuthenticateAsync(_incorrectOtp, sendResult.Token.Value, _ct);

        _ = result.ShouldBeOfType<OtpAuthenticationResult.Failure>();
    }

    [Fact]
    public async Task Cannot_attempt_to_authenticate_endlessly()
    {
        var sendResult = (await _authenticator.TrySendOtpAsync(TestData.CreateOtpAddress(), _ct)).ShouldNotBeNull();
        sendResult.Sent.ShouldBeTrue();
        _otpSender.Calls.ShouldNotBeEmpty();
        var correctOtp = _otpSender.Calls.First().Otp;
        _ = await _authenticator.TryAuthenticateAsync(_incorrectOtp, sendResult.Token.Value, _ct);
        _ = await _authenticator.TryAuthenticateAsync(_incorrectOtp, sendResult.Token.Value, _ct);
        _ = await _authenticator.TryAuthenticateAsync(_incorrectOtp, sendResult.Token.Value, _ct);
        _ = await _authenticator.TryAuthenticateAsync(_incorrectOtp, sendResult.Token.Value, _ct);
        _ = await _authenticator.TryAuthenticateAsync(_incorrectOtp, sendResult.Token.Value, _ct);

        var result = await _authenticator.TryAuthenticateAsync(correctOtp, sendResult.Token.Value, _ct);

        _ = result.ShouldBeOfType<OtpAuthenticationResult.Failure>();
    }

    [Fact]
    public async Task Cannot_authenticate_after_some_time()
    {
        var sendResult = (await _authenticator.TrySendOtpAsync(TestData.CreateOtpAddress(), _ct)).ShouldNotBeNull();
        sendResult.Sent.ShouldBeTrue();
        _otpSender.Calls.ShouldNotBeEmpty();
        var otp = _otpSender.Calls.First().Otp;
        _timeProvider.Advance(sendResult.ExpiresAfter.Value);

        var result = await _authenticator.TryAuthenticateAsync(otp, sendResult.Token.Value, _ct);

        _ = result.ShouldBeOfType<OtpAuthenticationResult.Failure>();
    }

    [Fact]
    public async Task Cannot_authenticate_again_without_sending_again()
    {
        var address = TestData.CreateOtpAddress();
        var sendResult = (await _authenticator.TrySendOtpAsync(address, _ct)).ShouldNotBeNull();
        sendResult.Sent.ShouldBeTrue();
        _otpSender.Calls.ShouldNotBeEmpty();
        var otp = _otpSender.Calls.First().Otp;
        _ = (await _authenticator.TryAuthenticateAsync(otp, sendResult.Token.Value, _ct)).ShouldBeOfType<OtpAuthenticationResult.Success>();

        var result = await _authenticator.TryAuthenticateAsync(otp, sendResult.Token.Value, _ct);

        _ = result.ShouldBeOfType<OtpAuthenticationResult.Failure>();
    }
}
