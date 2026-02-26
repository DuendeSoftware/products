// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Validator for handling DPoP proofs.
/// </summary>
public interface IDPoPProofValidator
{
    /// <summary>
    /// Validates the DPoP proof.
    /// </summary>
    /// <param name="context">The validation context.</param>
    /// <param name="ct">The cancellation token.</param>
    Task<DPoPProofValidatonResult> ValidateAsync(DPoPProofValidatonContext context, Ct ct);
}
