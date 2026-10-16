// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Formats.Cbor;
using System.Security.Cryptography;
using Duende.Platform.UserManagement.Fixtures;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
    public async Task duplicate_built_in_format_prevents_host_startup()
    {
        using var host = CreateHost(authentication =>
            _ = authentication.AddAttestationFormatValidator<DuplicatePackedAttestationFormatValidator>());

        var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync(_ct));

        exception.Message.ShouldBe("Multiple passkey attestation validators are registered for format 'packed'.");
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public async Task invalid_format_prevents_host_startup(string? format)
    {
        using var host = CreateHost(authentication =>
            _ = authentication.Services.AddSingleton<IAttestationFormatValidator>(
                new InvalidFormatAttestationValidator(format)));

        var exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync(_ct));

        exception.Message.ShouldBe(
            $"Passkey attestation validator '{typeof(InvalidFormatAttestationValidator).FullName}' must have a non-empty Format.");
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.IsCancellationRequested.ShouldBeFalse();
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
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var attestationObject = WebAuthnFixtures.CreateAttestationObjectWithEcdsa(
            FakeAttestationFormatValidator.FormatName, "example.com", credentialId, ecdsa);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(
            session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.AttestationValidationFailed);
        failure.ErrorDescription.ShouldBe("fake attestation failed");

        var context = serviceProvider.GetRequiredService<CapturedContext>().Value.ShouldNotBeNull();
        var reader = new CborReader(context.CredentialPublicKeyCbor.ToArray());
        reader.ReadStartMap().ShouldBe(5);

        var keyFields = new Dictionary<int, int>();
        var coordinates = new Dictionary<int, byte[]>();
        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var label = reader.ReadInt32();
            if (label is -2 or -3)
            {
                coordinates.Add(label, reader.ReadByteString());
            }
            else
            {
                keyFields.Add(label, reader.ReadInt32());
            }
        }

        reader.ReadEndMap();
        reader.BytesRemaining.ShouldBe(0);

        var expectedKey = ecdsa.ExportParameters(includePrivateParameters: false);
        keyFields[1].ShouldBe(2); // kty: EC2
        keyFields[3].ShouldBe(CoseAlgorithms.Es256);
        keyFields[-1].ShouldBe(1); // crv: P-256
        coordinates[-2].ShouldBe(expectedKey.Q.X);
        coordinates[-3].ShouldBe(expectedKey.Q.Y);
    }

    [Fact]
    public async Task null_validator_result_fails_registration()
    {
        await using var serviceProvider = await CreateServiceProviderWithCustomValidator<NullAttestationFormatValidator>();

        var passkeyAuth = serviceProvider.GetRequiredService<IPasskeyCeremonies>();
        var subjectId = (await serviceProvider.GetRequiredService<IExternalAuthenticator>().TryAuthenticateAsync(TestData.CreateExternalAuthenticatorAddress(), _ct)).ShouldBeOfType<ExternalAuthenticationResult.Success>().UserSubjectId;

        var session = await passkeyAuth.BeginRegistrationAsync(subjectId, "user@example.com", "Test User", _ct);

        var clientData = WebAuthnFixtures.CreateClientDataJson(
            PasskeyConstants.ClientDataType.Create, session.Options.Challenge, "https://example.com");
        var credentialId = RandomNumberGenerator.GetBytes(32);
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var attestationObject = WebAuthnFixtures.CreateAttestationObjectWithEcdsa(
            NullAttestationFormatValidator.FormatName, "example.com", credentialId, ecdsa);
        var request = WebAuthnFixtures.CreateCompleteRegistrationRequest(
            session.ChallengeId, clientData, attestationObject, credentialId, "Test Passkey");

        var result = await passkeyAuth.CompleteRegistrationAsync(request, _ct);

        var failure = result.ShouldBeOfType<PasskeyRegistrationCompleteResult.Failure>();
        failure.Error.ShouldBe(RegistrationError.AttestationValidationFailed);
        failure.ErrorDescription.ShouldBe("Attestation validator returned an invalid result.");
    }

    private static IHost CreateHost(Action<IUserAuthenticationBuilder> configure) =>
        new HostBuilder()
            .UseDefaultServiceProvider(options => options.ValidateScopes = true)
            .ConfigureServices(services =>
            {
                var userServices = UsersServiceProviderFactory.CreateUsersBuilder(
                    configureOptions: null, addDataProtection: false,
                    configureBuilder: builder => _ = builder.Authentication(configure));
                foreach (var service in userServices)
                {
                    services.Add(service);
                }
            })
            .Build();

    private static async Task<ServiceProvider> CreateServiceProviderWithCustomValidator<TValidator>()
        where TValidator : class, IAttestationFormatValidator =>
        await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: null, addDataProtection: false,
            configureServices: services => _ = services.AddSingleton<CapturedContext>(),
            configureBuilder: builder => _ = builder.Authentication(authentication =>
                _ = authentication.AddAttestationFormatValidator<TValidator>()));

    private sealed class CapturedContext
    {
        internal AttestationContext? Value { get; set; }
    }

    private sealed class FakeAttestationFormatValidator(CapturedContext capturedContext) : IAttestationFormatValidator
    {
        internal const string FormatName = "fake";

        public string Format => FormatName;

        public ValueTask<AttestationValidationResult> ValidateAsync(AttestationContext context, Ct ct)
        {
            capturedContext.Value = context;
            return new(new AttestationValidationResult.Failure(AttestationValidationError.AaguidMismatch,
                "fake attestation failed"));
        }
    }

    private sealed class DuplicatePackedAttestationFormatValidator : IAttestationFormatValidator
    {
        public string Format => PasskeyConstants.AttestationFormat.Packed;

        public ValueTask<AttestationValidationResult> ValidateAsync(AttestationContext context, Ct ct) =>
            throw new InvalidOperationException("Duplicate format validators must not be invoked.");
    }

    private sealed class InvalidFormatAttestationValidator(string? format) : IAttestationFormatValidator
    {
        public string Format => format!;

        public ValueTask<AttestationValidationResult> ValidateAsync(AttestationContext context, Ct ct) =>
            throw new InvalidOperationException("Startup validation must not validate an attestation statement.");
    }

    private sealed class NullAttestationFormatValidator : IAttestationFormatValidator
    {
        internal const string FormatName = "null-result";

        public string Format => FormatName;

        public ValueTask<AttestationValidationResult> ValidateAsync(AttestationContext context, Ct ct) => default;
    }
}
