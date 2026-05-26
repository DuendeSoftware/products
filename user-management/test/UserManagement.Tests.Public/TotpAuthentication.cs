// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Totp;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class TotpAuthentication : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private ITotpAuth _auth = null!;
    private IUserAuthenticatorsSelfService _selfService = null!;
    private ServiceProvider _serviceProvider = null!;
    private FakeTimeProvider _timeProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        _auth = _serviceProvider.GetRequiredService<ITotpAuth>();
        _selfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _timeProvider = _serviceProvider.GetRequiredService<FakeTimeProvider>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    [Theory]
    [InlineData(TestData.UnixTimeSeconds2005Minus60, TestData.Totp2005Minus60)]
    [InlineData(TestData.UnixTimeSeconds2005Minus30, TestData.Totp2005Minus30)]
    [InlineData(TestData.UnixTimeSeconds2005, TestData.Totp2005)]
    [InlineData(TestData.UnixTimeSeconds2005Plus30, TestData.Totp2005Plus30)]
    [InlineData(TestData.UnixTimeSeconds2005Plus60, TestData.Totp2005Plus60)]
    [InlineData(TestData.UnixTimeSeconds2009, TestData.Totp2009)]
    [InlineData(TestData.UnixTimeSeconds2033, TestData.Totp2033)]
    [InlineData(TestData.UnixTimeSeconds2603, TestData.Totp2603)]
    public async Task CanAuthenticate(long unixTimeSeconds, string totp)
    {
        var subjectId = await _selfService.CreateUserWithTotpAuthenticator(TestData.UnixTimeSeconds2000, PlainTextTotp.Create(TestData.Totp2000), _timeProvider, _ct);
        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds));

        var result = await _auth.TryAuthenticateAsync(subjectId, TotpAuthenticatorName.Default, PlainTextTotp.Create(totp), _ct);

        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(TestData.UnixTimeSeconds2005Minus60, TestData.Totp2005Minus60)]
    [InlineData(TestData.UnixTimeSeconds2005Minus30, TestData.Totp2005Minus30)]
    [InlineData(TestData.UnixTimeSeconds2005, TestData.Totp2005)]
    [InlineData(TestData.UnixTimeSeconds2005Plus30, TestData.Totp2005Plus30)]
    [InlineData(TestData.UnixTimeSeconds2005Plus60, TestData.Totp2005Plus60)]
    [InlineData(TestData.UnixTimeSeconds2009, TestData.Totp2009)]
    [InlineData(TestData.UnixTimeSeconds2033, TestData.Totp2033)]
    [InlineData(TestData.UnixTimeSeconds2603, TestData.Totp2603)]
    public async Task CannotAuthenticateMoreThanOnceWithAGivenTotp(long unixTimeSeconds, string totp)
    {
        var subjectId = await _selfService.CreateUserWithTotpAuthenticator(TestData.UnixTimeSeconds2000, PlainTextTotp.Create(TestData.Totp2000), _timeProvider, _ct);
        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds));
        (await _auth.TryAuthenticateAsync(subjectId, TotpAuthenticatorName.Default, PlainTextTotp.Create(totp), _ct)).ShouldBeTrue();

        var result = await _auth.TryAuthenticateAsync(subjectId, TotpAuthenticatorName.Default, PlainTextTotp.Create(totp), _ct);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task Cannot_authenticate_with_incorrect_totp()
    {
        var subjectId = await _selfService.CreateUserWithTotpAuthenticator(TestData.UnixTimeSeconds2000, PlainTextTotp.Create(TestData.Totp2000), _timeProvider, _ct);
        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2005));

        var result = await _auth.TryAuthenticateAsync(subjectId, TotpAuthenticatorName.Default, PlainTextTotp.Create("123456"), _ct);

        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData(TestData.Totp2005Minus30)]
    [InlineData(TestData.Totp2005Plus30)]
    public async Task CanAuthenticateWithAcceptableClockDifferences(string totp)
    {
        var subjectId = await _selfService.CreateUserWithTotpAuthenticator(TestData.UnixTimeSeconds2000, PlainTextTotp.Create(TestData.Totp2000), _timeProvider, _ct);
        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2005));

        var result = await _auth.TryAuthenticateAsync(subjectId, TotpAuthenticatorName.Default, PlainTextTotp.Create(totp), _ct);

        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(TestData.Totp2005Minus60)]
    [InlineData(TestData.Totp2005Plus60)]
    public async Task CannotAuthenticateWithUnacceptableClockDifferences(string totp)
    {
        var subjectId = await _selfService.CreateUserWithTotpAuthenticator(TestData.UnixTimeSeconds2000, PlainTextTotp.Create(TestData.Totp2000), _timeProvider, _ct);
        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2005));

        var result = await _auth.TryAuthenticateAsync(subjectId, TotpAuthenticatorName.Default, PlainTextTotp.Create(totp), _ct);

        result.ShouldBeFalse();
    }
}
