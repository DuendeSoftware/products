// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.QrCodes;
using Duende.UserManagement.Authentication.RecoveryCodes;
using Duende.UserManagement.Authentication.Totp;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class TotpAndRecoveryCodesHowToTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private ITotpAuthenticator _totpAuth = null!;
    private IRecoveryCodeAuthenticator _recoveryCodeAuth = null!;
    private IUserAuthenticatorsSelfService _selfService = null!;
    private IExternalAuthenticator _externalAuthenticator = null!;
    private ServiceProvider _serviceProvider = null!;
    private FakeTimeProvider _timeProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateWithOptionsAsync(options =>
        {
            // Recovery codes generate 10 codes; authenticating several in sequence exceeds the default velocity threshold.
            options.Throttling.MaxAttemptsPerWindow = 20;
        });
        _totpAuth = _serviceProvider.GetRequiredService<ITotpAuthenticator>();
        _recoveryCodeAuth = _serviceProvider.GetRequiredService<IRecoveryCodeAuthenticator>();
        _selfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _externalAuthenticator = _serviceProvider.GetRequiredService<IExternalAuthenticator>();
        _timeProvider = _serviceProvider.GetRequiredService<FakeTimeProvider>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static string BuildOtpauthUri(PlainBytesTotpKey key, string accountIdentifier)
    {
        // begin-snippet: totp-create-uri
        var uri = TotpAuthenticatorUri.Generate("Contoso", accountIdentifier, key);
        // end-snippet
        return uri;
    }

    private static string RenderQrCodeSvg(string otpauthUri)
    {
        // begin-snippet: totp-render-qr
        var symbol = QrEncoder.Encode(otpauthUri);
        var svg = QrSvgRenderer.Render(symbol);
        // end-snippet
        return svg;
    }

    private static async Task<bool> EnrolDeviceAsync(
        IUserAuthenticatorsSelfService selfService, UserSubjectId subjectId, PlainBytesTotpKey key, PlainTextTotp totp, Ct ct)
    {
        // begin-snippet: totp-enrol
        var enrolled = await selfService.TryAddTotpDeviceAsync(subjectId, TotpDeviceName.Default, key, totp, ct);
        // end-snippet
        return enrolled;
    }

    private static async Task<bool> VerifyDeviceAsync(ITotpAuthenticator auth, UserSubjectId subjectId, PlainTextTotp totp, Ct ct)
    {
        // begin-snippet: totp-verify
        var verified = await auth.TryAuthenticateAsync(subjectId, TotpDeviceName.Default, totp, ct);
        // end-snippet
        return verified;
    }

    private static async Task<bool> RemoveDeviceAsync(IUserAuthenticatorsSelfService selfService, UserSubjectId subjectId, Ct ct)
    {
        // begin-snippet: totp-remove-device
        var removed = await selfService.TryRemoveTotpDeviceAsync(subjectId, TotpDeviceName.Default, ct);
        // end-snippet
        return removed;
    }

    private static async Task<IReadOnlyCollection<PlainTextRecoveryCode>?> CreateRecoveryCodesAsync(
        IUserAuthenticatorsSelfService selfService, UserSubjectId subjectId, Ct ct)
    {
        // begin-snippet: recovery-codes-create
        var codes = await selfService.TryCreateRecoveryCodesAsync(subjectId, ct);
        // end-snippet
        return codes;
    }

    private static async Task<bool> UseRecoveryCodeAsync(
        IRecoveryCodeAuthenticator auth, UserSubjectId subjectId, PlainTextRecoveryCode code, Ct ct)
    {
        // begin-snippet: recovery-codes-use
        var authenticated = await auth.TryAuthenticateAsync(subjectId, code, ct);
        // end-snippet
        return authenticated;
    }
#pragma warning restore IDE0022

    [Fact]
    public void Can_generate_an_otpauth_uri_and_render_it_as_a_QR_code()
    {
        var uri = BuildOtpauthUri(TestData.TotpKey, "alice@example.com");

        uri.ShouldStartWith("otpauth://totp/Contoso:alice%40example.com?secret=");

        var svg = RenderQrCodeSvg(uri);

        svg.ShouldStartWith("<svg ");
        svg.ShouldContain("</svg>");
    }

    [Fact]
    public async Task Can_enrol_a_device_and_verify_a_code_against_it()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2000));

        var enrolled = await EnrolDeviceAsync(_selfService, subjectId, TestData.TotpKey, PlainTextTotp.Create(TestData.Totp2000), _ct);

        enrolled.ShouldBeTrue();

        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2005));

        var verified = await VerifyDeviceAsync(_totpAuth, subjectId, PlainTextTotp.Create(TestData.Totp2005), _ct);

        verified.ShouldBeTrue();
    }

    [Fact]
    public async Task Enrolling_with_an_incorrect_code_fails()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2000));

        var enrolled = await EnrolDeviceAsync(_selfService, subjectId, TestData.TotpKey, PlainTextTotp.Create("000000"), _ct);

        enrolled.ShouldBeFalse();

        var record = (await _selfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull();
        record.TotpDeviceNames.ShouldBeEmpty();
    }

    [Fact]
    public async Task Can_remove_a_device()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2000));
        (await EnrolDeviceAsync(_selfService, subjectId, TestData.TotpKey, PlainTextTotp.Create(TestData.Totp2000), _ct)).ShouldBeTrue();

        var removed = await RemoveDeviceAsync(_selfService, subjectId, _ct);

        removed.ShouldBeTrue();
        var record = (await _selfService.TryGetAsync(subjectId, _ct)).ShouldNotBeNull();
        record.TotpDeviceNames.ShouldBeEmpty();

        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2005));
        var verifiedAfterRemoval = await VerifyDeviceAsync(_totpAuth, subjectId, PlainTextTotp.Create(TestData.Totp2005), _ct);

        verifiedAfterRemoval.ShouldBeFalse();
    }

    [Fact]
    public async Task Can_create_and_use_recovery_codes_and_cannot_reuse_one()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);

        var codes = (await CreateRecoveryCodesAsync(_selfService, subjectId, _ct)).ShouldNotBeNull();

        codes.Count.ShouldBe(10);

        var firstCode = codes.ElementAt(0);
        var usedOnce = await UseRecoveryCodeAsync(_recoveryCodeAuth, subjectId, firstCode, _ct);

        usedOnce.ShouldBeTrue();

        var usedAgain = await UseRecoveryCodeAsync(_recoveryCodeAuth, subjectId, firstCode, _ct);

        usedAgain.ShouldBeFalse();
    }
}
