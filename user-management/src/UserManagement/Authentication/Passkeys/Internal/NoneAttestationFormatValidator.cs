// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Validates "none" attestation format (self-attestation).
/// </summary>
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class NoneAttestationFormatValidator : IAttestationFormatValidator
{
    public string Format => PasskeyConstants.AttestationFormat.None;

    public ValueTask<AttestationValidationResult> ValidateAsync(AttestationContext context, Ct ct)
    {
        // https://www.w3.org/TR/webauthn-3/#sctn-none-attestation
        if (context.AttStmt.Count != 0)
        {
            return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Failure(
                AttestationValidationError.InvalidAttestationStatement,
                "Attestation statement must be empty for 'none' format."));
        }

        return ValueTask.FromResult<AttestationValidationResult>(new AttestationValidationResult.Success(null));
    }
}
