// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Validates DPoP Proofs.
/// </summary>
public interface IDPoPProofValidator
{
    /// <summary>
    /// Validates the DPoP proof.
    /// </summary>
    Task<DPoPProofValidationResult> Validate(DPoPProofValidationContext context, CT ct = default);
}
