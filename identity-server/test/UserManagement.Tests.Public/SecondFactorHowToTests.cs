// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;
using System.Security.Cryptography;
using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.RecoveryCodes;
using Duende.UserManagement.Authentication.Totp;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class SecondFactorHowToTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private IPasskeyCeremonies _ceremonies = null!;
    private IUserAuthenticatorsSelfService _selfService = null!;
    private ITotpAuthenticator _totpAuth = null!;
    private IRecoveryCodeAuthenticator _recoveryCodeAuth = null!;
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
        _ceremonies = _serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        _selfService = _serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        _totpAuth = _serviceProvider.GetRequiredService<ITotpAuthenticator>();
        _recoveryCodeAuth = _serviceProvider.GetRequiredService<IRecoveryCodeAuthenticator>();
        _externalAuthenticator = _serviceProvider.GetRequiredService<IExternalAuthenticator>();
        _timeProvider = _serviceProvider.GetRequiredService<FakeTimeProvider>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static async Task<PasskeyCredentialData?> RegisterPasskeyAsync(
        IPasskeyCeremonies ceremonies,
        IUserAuthenticatorsSelfService selfService,
        UserSubjectId subjectId,
        byte[] credentialId,
        ECDsa authenticatorKey,
        string origin,
        Ct ct)
    {
        var session = await ceremonies.BeginRegistrationAsync(subjectId, "user@example.com", "Example User", ct);

        // The browser's WebAuthn API builds this from the authenticator's response; WebAuthnFixtures
        // builds an equivalent response here, standing in for a real authenticator.
        var clientData = WebAuthnFixtures.CreateClientDataJson(
            PasskeyConstants.ClientDataType.Create, session.Options.Challenge, origin);
        var attestationObject = WebAuthnFixtures.CreateAttestationObjectWithEcdsa(
            PasskeyConstants.AttestationFormat.None, new Uri(origin).Host, credentialId, authenticatorKey, flags: 0x45);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(
            session.ChallengeId, clientData, attestationObject, credentialId, "My Passkey");

        var result = await ceremonies.CompleteRegistrationAsync(request, ct);
        if (result is not PasskeyRegistrationCompleteResult.Success success)
        {
            return null;
        }

        _ = await selfService.TryAddPasskeyAsync(subjectId, success.Credential, ct);
        return success.Credential;
    }

    private static async Task<PasskeyAuthenticationCompleteResult> AuthenticateSecondFactorAsync(
        IPasskeyCeremonies ceremonies,
        FirstFactorSubjectResolver resolver,
        byte[] credentialId,
        ECDsa authenticatorKey,
        string origin,
        Ct ct)
    {
        var subjectId = await resolver.ResolveAsync(ct) ?? throw new InvalidOperationException("No first-factor subject.");
        var beginResult = await ceremonies.BeginAuthenticationAsync(subjectId, ct);
        var session = ((PasskeyAuthenticationBeginResult.Success)beginResult).Session;

        // The browser's WebAuthn API builds this from the authenticator's response; WebAuthnFixtures
        // builds an equivalent response here, standing in for a real authenticator.
        var clientData = WebAuthnFixtures.CreateClientDataJson(
            PasskeyConstants.ClientDataType.Get, session.Options.Challenge, origin);
        var authenticatorData = WebAuthnFixtures.CreateAuthenticatorData(new Uri(origin).Host, flags: 0x01, signCount: 1);
        var clientDataBytes = WebAuthnFixtures.DecodeBase64Url(clientData);
        var signature = WebAuthnFixtures.CreateValidSignature(authenticatorKey, authenticatorData, clientDataBytes);
        var request = new PasskeyCompleteAuthenticationRequest
        {
            ChallengeId = session.ChallengeId,
            Id = Base64Url.EncodeToString(credentialId),
            RawId = Base64Url.EncodeToString(credentialId),
            Type = PasskeyConstants.CredentialType.PublicKey,
            Response = new AuthenticatorAssertionResponse
            {
                ClientDataJSON = clientData,
                AuthenticatorData = Base64Url.EncodeToString(authenticatorData),
                Signature = signature
            }
        };

        return await ceremonies.CompleteAuthenticationAsync(request, ct);
    }

    private static async Task<UserAuthenticators?> CheckEnrolledFactorsAsync(
        IUserAuthenticatorsSelfService selfService, UserSubjectId subjectId, Ct ct)
    {
        // begin-snippet: mfa-check-enrolled-factors
        var authenticators = await selfService.TryGetAsync(subjectId, ct);
        // end-snippet
        return authenticators;
    }

    private static async Task<bool> VerifyTotpSecondFactorAsync(
        ITotpAuthenticator totpAuth, UserSubjectId subjectId, PlainTextTotp totp, Ct ct)
    {
        // begin-snippet: mfa-verify-totp-second-factor
        var verified = await totpAuth.TryAuthenticateAsync(subjectId, TotpDeviceName.Default, totp, ct);
        // end-snippet
        return verified;
    }

    private static async Task<bool> VerifyRecoveryCodeSecondFactorAsync(
        IRecoveryCodeAuthenticator recoveryCodeAuth, UserSubjectId subjectId, PlainTextRecoveryCode code, Ct ct)
    {
        // begin-snippet: mfa-verify-recovery-code
        var authenticated = await recoveryCodeAuth.TryAuthenticateAsync(subjectId, code, ct);
        // end-snippet
        return authenticated;
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Resolver_subject_scopes_the_passkey_ceremony_to_that_users_credentials()
    {
        var subjectA = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var subjectB = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        using var keyA = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var keyB = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var credentialIdA = RandomNumberGenerator.GetBytes(32);
        var credentialIdB = RandomNumberGenerator.GetBytes(32);
        _ = (await RegisterPasskeyAsync(_ceremonies, _selfService, subjectA, credentialIdA, keyA, "https://example.com", _ct))
            .ShouldNotBeNull();
        _ = (await RegisterPasskeyAsync(_ceremonies, _selfService, subjectB, credentialIdB, keyB, "https://example.com", _ct))
            .ShouldNotBeNull();

        var resolver = new FirstFactorSubjectResolver(() => subjectA);

        // Completing with subject B's credential, while the ceremony began for subject A, fails: the
        // ceremony is scoped to the resolver's subject, not to whichever credential the request presents.
        var crossUserResult = await AuthenticateSecondFactorAsync(_ceremonies, resolver, credentialIdB, keyB, "https://example.com", _ct);
        _ = crossUserResult.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Failure>();

        var sameUserResult = await AuthenticateSecondFactorAsync(_ceremonies, resolver, credentialIdA, keyA, "https://example.com", _ct);
        var success = sameUserResult.ShouldBeOfType<PasskeyAuthenticationCompleteResult.Success>();
        success.UserSubjectId.ShouldBe(subjectA);
    }

    [Fact]
    public async Task Can_check_which_second_factors_a_user_has_enrolled()
    {
        var subjectId = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);

        var beforeEnrolment = (await CheckEnrolledFactorsAsync(_selfService, subjectId, _ct)).ShouldNotBeNull();
        beforeEnrolment.TotpDeviceNames.ShouldBeEmpty();
        beforeEnrolment.RecoveryCodeCount.ShouldBe(0);

        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2000));
        _ = await _selfService.TryAddTotpDeviceAsync(
            subjectId, TotpDeviceName.Default, TestData.TotpKey, PlainTextTotp.Create(TestData.Totp2000), _ct);
        _ = await _selfService.TryCreateRecoveryCodesAsync(subjectId, _ct);

        var afterEnrolment = (await CheckEnrolledFactorsAsync(_selfService, subjectId, _ct)).ShouldNotBeNull();
        afterEnrolment.TotpDeviceNames.ShouldContain(TotpDeviceName.Default);
        afterEnrolment.RecoveryCodeCount.ShouldBe(10);
    }

    [Fact]
    public async Task Totp_succeeds_as_a_second_factor_only_for_the_resolved_subject()
    {
        var subjectA = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var subjectB = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2000));
        _ = await _selfService.TryAddTotpDeviceAsync(
            subjectA, TotpDeviceName.Default, TestData.TotpKey, PlainTextTotp.Create(TestData.Totp2000), _ct);

        var resolver = new FirstFactorSubjectResolver(() => subjectA);
        var resolvedSubjectId = (await resolver.ResolveAsync(_ct)).ShouldNotBeNull();

        _timeProvider.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds((long)TestData.UnixTimeSeconds2005));
        var verifiedForResolvedSubject = await VerifyTotpSecondFactorAsync(_totpAuth, resolvedSubjectId, PlainTextTotp.Create(TestData.Totp2005), _ct);
        verifiedForResolvedSubject.ShouldBeTrue();

        var verifiedForOtherSubject = await VerifyTotpSecondFactorAsync(_totpAuth, subjectB, PlainTextTotp.Create(TestData.Totp2005), _ct);
        verifiedForOtherSubject.ShouldBeFalse();
    }

    [Fact]
    public async Task Recovery_code_succeeds_as_a_second_factor_only_for_the_resolved_subject()
    {
        var subjectA = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var subjectB = await _externalAuthenticator.CreateUserAsync(TestData.CreateExternalAuthenticatorAddress(), _ct);
        var codes = (await _selfService.TryCreateRecoveryCodesAsync(subjectA, _ct)).ShouldNotBeNull();
        var code = codes.ElementAt(0);

        var resolver = new FirstFactorSubjectResolver(() => subjectA);
        var resolvedSubjectId = (await resolver.ResolveAsync(_ct)).ShouldNotBeNull();

        var verifiedForOtherSubject = await VerifyRecoveryCodeSecondFactorAsync(_recoveryCodeAuth, subjectB, code, _ct);
        verifiedForOtherSubject.ShouldBeFalse();

        var verifiedForResolvedSubject = await VerifyRecoveryCodeSecondFactorAsync(_recoveryCodeAuth, resolvedSubjectId, code, _ct);
        verifiedForResolvedSubject.ShouldBeTrue();
    }
}

// begin-snippet: mfa-second-factor-resolver
/// <summary>
/// Resolves the user who completed the first authentication factor, for the passkey second-factor ceremony.
/// </summary>
/// <remarks>
/// The login page is responsible for tracking which user completed the first factor (for example in a
/// temporary cookie or session) before the second factor begins. <paramref name="currentFirstFactorSubject"/>
/// stands in for however that tracking works.
/// </remarks>
internal sealed class FirstFactorSubjectResolver(Func<UserSubjectId?> currentFirstFactorSubject) : ISecondFactorPasskeyAuthenticationResolver
{
    public Task<UserSubjectId?> ResolveAsync(Ct ct) => Task.FromResult(currentFirstFactorSubject());
}
// end-snippet
