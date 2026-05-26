// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;

namespace Duende.Platform.UserManagement.Passkeys;

public static class NoneAttestationFormatValidatorTests
{
    [Fact]
    public static void Format_returns_none()
    {
        var validator = new NoneAttestationFormatValidator();

        validator.Format.ShouldBe(PasskeyConstants.AttestationFormat.None);
    }

    [Fact]
    public static async Task Validate_empty_AttStmt_returns_success()
    {
        var validator = new NoneAttestationFormatValidator();
        var context = new AttestationContext(
            AttStmt: new Dictionary<string, object?>(),
            AuthData: Array.Empty<byte>(),
            ClientDataHash: Array.Empty<byte>(),
            CredentialPublicKey: new CoseKey(0, 0, Array.Empty<byte>()));

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<AttestationValidationResult.Success>();
    }

    [Fact]
    public static async Task Validate_non_empty_AttStmt_returns_failure()
    {
        var validator = new NoneAttestationFormatValidator();
        var context = new AttestationContext(
            AttStmt: new Dictionary<string, object?>
            {
                ["unexpected"] = "value"
            },
            AuthData: Array.Empty<byte>(),
            ClientDataHash: Array.Empty<byte>(),
            CredentialPublicKey: new CoseKey(0, 0, Array.Empty<byte>()));

        var result = await validator.ValidateAsync(context, TestContext.Current.CancellationToken);

        var failure = result.ShouldBeOfType<AttestationValidationResult.Failure>();
        failure.Error.ShouldBe(AttestationValidationError.InvalidAttestationStatement);
        ShouldlyExtensions.ShouldContain(failure.ErrorDescription, "empty");
    }
}
