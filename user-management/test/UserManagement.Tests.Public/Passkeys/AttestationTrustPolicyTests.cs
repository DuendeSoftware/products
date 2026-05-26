// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement.Passkeys;

public sealed class AttestationTrustPolicyTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Registration_should_succeed_when_no_policies_are_registered()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateAsync();
        var result = await RegisterPasskeyAsync(serviceProvider);

        _ = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
    }

    [Fact]
    public async Task Registration_should_succeed_when_policy_accepts()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateAsync(builder =>
        {
            _ = builder.EnableAuthentication(auth => { _ = auth.AddAttestationTrustPolicy<AcceptAllPolicy>(); });
        });

        var result = await RegisterPasskeyAsync(serviceProvider);

        _ = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
    }

    [Fact]
    public async Task Registration_should_fail_when_policy_rejects()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateAsync(builder =>
        {
            _ = builder.EnableAuthentication(auth => { _ = auth.AddAttestationTrustPolicy<RejectAllPolicy>(); });
        });

        var result = await RegisterPasskeyAsync(serviceProvider);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.AttestationTrustPolicyFailed);
        failure.ErrorDescription.ShouldBe("authenticator is not trusted");
    }

    [Fact]
    public async Task Registration_should_succeed_when_all_policies_accept()
    {
        await using var serviceProvider = await UsersServiceProviderFactory.CreateAsync(builder =>
        {
            _ = builder.EnableAuthentication(auth =>
            {
                _ = auth.AddAttestationTrustPolicy<AcceptAllPolicy>();
                _ = auth.AddAttestationTrustPolicy<AlsoAcceptAllPolicy>();
            });
        });

        var result = await RegisterPasskeyAsync(serviceProvider);

        _ = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
    }

    [Fact]
    public async Task Registration_should_fail_when_any_policy_rejects()
    {
        var tracker = new InvocationTracker();

        await using var serviceProvider = await UsersServiceProviderFactory.CreateAsync(builder =>
        {
            _ = builder.EnableAuthentication(auth =>
            {
                _ = auth.AddAttestationTrustPolicy<RejectAllPolicy>();
                _ = auth.AddAttestationTrustPolicy<TrackingAcceptPolicy>();
                _ = auth.Services.AddSingleton(tracker);
            });
        });

        var result = await RegisterPasskeyAsync(serviceProvider);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.AttestationTrustPolicyFailed);
        failure.ErrorDescription.ShouldBe("authenticator is not trusted");
        tracker.InvocationCount.ShouldBe(0);
    }
    [Fact]
    public async Task Should_populate_context_with_aaguid_and_format()
    {
        var expectedAaguid = Guid.Parse("f1d0f1d0-f1d0-f1d0-f1d0-f1d0f1d0f1d0");
        var capturedContext = new CapturedContext();

        await using var serviceProvider = await UsersServiceProviderFactory.CreateAsync(builder =>
        {
            _ = builder.EnableAuthentication(auth =>
            {
                _ = auth.AddAttestationTrustPolicy<CapturingPolicy>();
                _ = auth.Services.AddSingleton(capturedContext);
            });
        });

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var user = (await selfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct))
            .ShouldNotBeNull();

        var session = await passkeyAuth.BeginRegistrationAsync(user.SubjectId, "user@example.com", "Test User", _ct);
        var clientData = WebAuthnFixtures.CreateClientDataJson(
            PasskeyConstants.ClientDataType.Create,
            session.Options.Challenge,
            "https://example.com");
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObjectWithEcdsa(
            PasskeyConstants.AttestationFormat.None,
            "example.com",
            credentialId,
            ecdsa,
            flags: 0x45,
            aaguid: expectedAaguid);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(
            session.ChallengeId,
            clientData,
            attestationObject,
            credentialId,
            "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        _ = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
        var context = capturedContext.Value.ShouldNotBeNull();
        context.Aaguid.ShouldBe(expectedAaguid);
        context.AttestationFormat.ShouldBe(PasskeyConstants.AttestationFormat.None);
        context.UserSubjectId.ShouldBe(user.SubjectId);
        context.CertificateChain.ShouldBeNull();
    }

    [Fact]
    public async Task Should_populate_context_with_certificate_chain_for_packed_attestation()
    {
        var expectedAaguid = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
        var capturedContext = new CapturedContext();

        await using var serviceProvider = await UsersServiceProviderFactory.CreateAsync(builder =>
        {
            _ = builder.EnableAuthentication(auth =>
            {
                _ = auth.AddAttestationTrustPolicy<CapturingPolicy>();
                _ = auth.Services.AddSingleton(capturedContext);
            });
        });

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var timeProvider = serviceProvider.GetRequiredService<TimeProvider>();
        var user = (await selfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct))
            .ShouldNotBeNull();

        var session = await passkeyAuth.BeginRegistrationAsync(user.SubjectId, "user@example.com", "Test User", _ct);
        var clientData = WebAuthnFixtures.CreateClientDataJson(
            PasskeyConstants.ClientDataType.Create,
            session.Options.Challenge,
            "https://example.com");

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var now = timeProvider.GetUtcNow();
        using var cert = WebAuthnFixtures.CreateAttestationCertificate(
            ecdsa, notBefore: now.AddDays(-1), notAfter: now.AddDays(365));
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreatePackedAttestationObject(
            "example.com",
            credentialId,
            ecdsa,
            cert,
            clientData,
            aaguid: expectedAaguid);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(
            session.ChallengeId,
            clientData,
            attestationObject,
            credentialId,
            "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        _ = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Success>();
        var context = capturedContext.Value.ShouldNotBeNull();
        context.Aaguid.ShouldBe(expectedAaguid);
        context.AttestationFormat.ShouldBe(PasskeyConstants.AttestationFormat.Packed);
        context.UserSubjectId.ShouldBe(user.SubjectId);
        _ = context.CertificateChain.ShouldNotBeNull();
        context.CertificateChain.ShouldNotBeEmpty();
    }

    private async Task<PasskeyRegistrationCompleteResult> RegisterPasskeyAsync(ServiceProvider serviceProvider)
    {
        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var selfService = serviceProvider.GetRequiredService<IUserAuthenticatorsSelfService>();
        var user = (await selfService.TryRegisterAsync(UserSubjectId.New(), TestData.CreateExternalAuthenticator(), _ct))
            .ShouldNotBeNull();

        var session = await passkeyAuth.BeginRegistrationAsync(user.SubjectId, "user@example.com", "Test User", _ct);
        var clientData = WebAuthnFixtures.CreateClientDataJson(
            PasskeyConstants.ClientDataType.Create,
            session.Options.Challenge,
            "https://example.com");
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObjectWithEcdsa(
            PasskeyConstants.AttestationFormat.None,
            "example.com",
            credentialId,
            ecdsa,
            flags: 0x45);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(
            session.ChallengeId,
            clientData,
            attestationObject,
            credentialId,
            "Test Passkey");

        return await passkeyAuth.CompleteRegistrationAsync(request, _ct);
    }

    private sealed class AcceptAllPolicy : IAttestationTrustPolicy
    {
        public ValueTask<AttestationTrustPolicyResult> EvaluateAsync(AttestationTrustContext context,
            CancellationToken ct) =>
            ValueTask.FromResult(AttestationTrustPolicyResult.Accept());
    }

    private sealed class AlsoAcceptAllPolicy : IAttestationTrustPolicy
    {
        public ValueTask<AttestationTrustPolicyResult> EvaluateAsync(AttestationTrustContext context,
            CancellationToken ct) =>
            ValueTask.FromResult(AttestationTrustPolicyResult.Accept());
    }

    private sealed class RejectAllPolicy : IAttestationTrustPolicy
    {
        public ValueTask<AttestationTrustPolicyResult> EvaluateAsync(AttestationTrustContext context,
            CancellationToken ct) =>
            ValueTask.FromResult(AttestationTrustPolicyResult.Reject("authenticator is not trusted"));
    }

    private sealed class TrackingAcceptPolicy(InvocationTracker tracker) : IAttestationTrustPolicy
    {
        public ValueTask<AttestationTrustPolicyResult> EvaluateAsync(AttestationTrustContext context,
            CancellationToken ct)
        {
            tracker.InvocationCount++;
            return ValueTask.FromResult(AttestationTrustPolicyResult.Accept());
        }
    }

    private sealed class InvocationTracker
    {
        public int InvocationCount { get; set; }
    }

    private sealed class CapturedContext
    {
        public AttestationTrustContext? Value { get; set; }
    }

    private sealed class CapturingPolicy(CapturedContext captured) : IAttestationTrustPolicy
    {
        public ValueTask<AttestationTrustPolicyResult> EvaluateAsync(AttestationTrustContext context,
            CancellationToken ct)
        {
            captured.Value = context;
            return ValueTask.FromResult(AttestationTrustPolicyResult.Accept());
        }
    }
}
