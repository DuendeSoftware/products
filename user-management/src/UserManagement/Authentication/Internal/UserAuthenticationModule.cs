// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Otp.Internal;
using Duende.UserManagement.Authentication.Otp.Internal.Storage;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Duende.UserManagement.Authentication.Passkeys.Internal.Storage;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Authentication.Passwords.Internal;
using Duende.UserManagement.Authentication.RecoveryCodes;
using Duende.UserManagement.Authentication.RecoveryCodes.Internal;
using Duende.UserManagement.Authentication.Totp;
using Duende.UserManagement.Authentication.Totp.Internal;
using Duende.UserManagement.Import.Internal;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Internal.Modules;
using Duende.UserManagement.Internal.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Internal;

internal sealed class UserAuthenticationModule : IDuendeModule
{
    public static void Register(IServiceCollection services)
    {
        services.RegisterModule<UserCoreModule>();
        services.RegisterModule<UserImportModule>();
        services.RegisterModule<StorageModule>();
        services.RegisterModule<UserAuthenticationWebModule>();

        services.RegisterFeature<UserAuthenticationFeature>();

        // 1. Register DSO types
        services.RegisterDsoType<UserAuthenticatorsDso.V1>();
        services.RegisterDsoType<OtpWorkflowDso.V1>();
        services.RegisterDsoType<PasskeyRegistrationChallengeDso.V1>();
        services.RegisterDsoType<PasskeyAuthenticationChallengeDso.V1>();

        // 2. Register authentication services
        _ = services.AddTransient<IOtpAuthenticator, OtpAuthenticator>();
        _ = services.AddTransient<IOtpSender, OtpSender>();
        _ = services.AddTransient<OtpVerifier>();
        _ = services.AddTransient<ITotpAuth, TotpAuth>();
        _ = services.AddTransient<IRecoveryCodeAuth, RecoveryCodeAuth>();
        _ = services.AddTransient<IPasswordAuth, PasswordAuth>();
        services.TryAddTransient<IAuthenticationAttemptPolicy, DefaultAuthenticationAttemptPolicy>();
        _ = services.AddSingleton<IPasswordHashAlgorithm, Pbkdf2Sha512PasswordHashAlgorithm>();
        _ = services.AddTransient<PasswordHashAlgorithms>();

        // 3. Register passkey services
        // Signature verifiers are registered in order of preference.
        _ = services.AddSingleton<ISignatureVerifier, Es256SignatureVerifier>();
        _ = services.AddSingleton<ISignatureVerifier, Es384SignatureVerifier>();
        _ = services.AddSingleton<ISignatureVerifier, Es512SignatureVerifier>();
        _ = services.AddSingleton<ISignatureVerifier, Rs256SignatureVerifier>();
        _ = services.AddSingleton<ISignatureVerifier, Rs384SignatureVerifier>();
        _ = services.AddSingleton<ISignatureVerifier, Rs512SignatureVerifier>();
        _ = services.AddSingleton<ISignatureVerifier, Ps256SignatureVerifier>();
        _ = services.AddSingleton<ISignatureVerifier, Ps384SignatureVerifier>();
        _ = services.AddSingleton<ISignatureVerifier, Ps512SignatureVerifier>();
        _ = services.AddSingleton<ISignatureVerifier, Rs1SignatureVerifier>();
        _ = services.AddTransient<IPasskeyCeremonies, PasskeyCeremonies>();
        _ = services.AddTransient<WebAuthnAuthenticationCeremony>();
        _ = services.AddTransient<WebAuthnRegistrationCeremony>();
        _ = services.AddTransient<IAttestationFormatValidator, NoneAttestationFormatValidator>();
        _ = services.AddTransient<IAttestationFormatValidator, PackedAttestationFormatValidator>();
        _ = services.AddTransient<IAttestationFormatValidator, TpmAttestationFormatValidator>();
        _ = services.AddSingleton<IPasskeyOriginValidator, DefaultPasskeyOriginValidator>();

        // 4. Register self-service
        _ = services.AddTransient<IUserAuthenticatorsSelfService, UserAuthenticatorsSelfService>();
        services.TryAddTransient<IUserSelfService, UserSelfService>();

        // 5. Register admin services
        _ = services.AddTransient<IUserAuthenticatorsAdmin, UserAuthenticatorsAdmin>();
        services.TryAddTransient<IUserAdmin, UserAdmin>();

        // 6. Register misc services
        services.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);
        services.TryAddSingleton<IOtpDispatcher, LogOtpDispatcher>();
        _ = services.AddTransient<ValidatedPlainTextPasswordFactory>();
        _ = services.AddTransient<IPasswordValidator, PasswordHistoryValidator>();

        // 7. Register repositories
        _ = services.AddScoped<OtpWorkflowRepository>();
        _ = services.AddScoped<UserAuthenticatorsRepository>();

        // 8. Register readers
        _ = services.AddScoped<IPasskeyRegistrationChallengeStore, PasskeyRegistrationChallengeRepository>();
        _ = services.AddScoped<IPasskeyAuthenticationChallengeStore, PasskeyAuthenticationChallengeRepository>();

        // 9. Register options
        _ = services.AddOptions<UserAuthenticationOptions>();
        _ = services.AddSingleton<IValidateOptions<UserAuthenticationOptions>, UserAuthenticationOptionsValidator>();
        _ = services.AddOptions<UserAuthenticatorsRepository.Options>()
            .Configure<IOptions<UserAuthenticationOptions>>((o, root) =>
                o.ProtectTotpKeys = root.Value.Totp.Storage.ProtectKeys);
    }
}
