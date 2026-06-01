// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using Duende.Platform.UserManagement.Passkeys.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement.Passkeys;

public sealed class CustomAttestationFormatValidatorTests
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Built_in_validators_should_be_present_alongside_custom_validator()
    {
        await using var serviceProvider =
            await CreateServiceProviderWithCustomValidator<FakeAttestationFormatValidator>();

        var validators = serviceProvider.GetServices<IAttestationFormatValidator>().ToList();

        // Built-in formats
        validators.ShouldContain(v => v.Format == PasskeyConstants.AttestationFormat.None);
        validators.ShouldContain(v => v.Format == PasskeyConstants.AttestationFormat.Packed);
        validators.ShouldContain(v => v.Format == PasskeyConstants.AttestationFormat.Tpm);

        validators.ShouldContain(v => v.Format == FakeAttestationFormatValidator.FormatName);

        validators.Count.ShouldBe(4);
    }

    [Fact]
    public async Task Custom_validator_is_invoked_during_registration()
    {
        await using var serviceProvider = await CreateServiceProviderWithCustomValidator<FakeAttestationFormatValidator>();

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var subjectId = (await serviceProvider.GetRequiredService<IExternalAuthenticator>().TryAuthenticateAsync(TestData.CreateExternalAuthenticatorAddress(), _ct)).ShouldBeOfType<ExternalAuthenticationResult.Success>().UserSubjectId;

        var session = await passkeyAuth.BeginRegistrationAsync(subjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(
            PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        var attestationObject = WebAuthnFixtures.CreateAttestationObject(
            FakeAttestationFormatValidator.FormatName, "example.com", credentialId);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(
            session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.ErrorDescription.ShouldBe("fake attestation failed");
    }

    private static async Task<ServiceProvider> CreateServiceProviderWithCustomValidator<TValidator>()
        where TValidator : class, IAttestationFormatValidator =>
        await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: null, addDataProtection: false,
            configureServices: services => _ = services.AddTransient<IAttestationFormatValidator, TValidator>());

    private sealed class FakeAttestationFormatValidator : IAttestationFormatValidator
    {
        internal const string FormatName = "fake";

        public string Format => FormatName;

        public ValueTask<AttestationValidationResult> ValidateAsync(AttestationContext context, Ct ct) =>
            new(new AttestationValidationResult.Failure(AttestationValidationError.AaguidMismatch,
                "fake attestation failed"));
    }
}
