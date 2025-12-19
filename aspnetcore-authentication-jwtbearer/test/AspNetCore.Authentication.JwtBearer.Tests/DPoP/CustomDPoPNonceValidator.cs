// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AspNetCore.Authentication.JwtBearer.DPoP;

namespace Duende.AspNetCore.Authentication.JwtBearer;

/// <summary>
/// An example of a custom nonce validator for testing that uses a simple counter-based nonce
/// </summary>
public class CustomDPoPNonceValidator : IDPoPNonceValidator
{
    private int _counter = 0;

    public string CreateNonce(DPoPProofValidationContext context)
    {
        // Create a simple nonce with a counter
        _counter++;
        return $"custom-nonce-{_counter}";
    }

    public NonceValidationResult ValidateNonce(DPoPProofValidationContext context, string? nonce)
    {
        if (string.IsNullOrWhiteSpace(nonce))
        {
            return NonceValidationResult.Missing;
        }

        // Validate that the nonce starts with our custom prefix
        if (!nonce.StartsWith("custom-nonce-"))
        {
            return NonceValidationResult.Invalid;
        }

        // For this test, we'll accept any nonce with the correct prefix
        // In a real implementation, you would validate expiration, etc.
        return NonceValidationResult.Valid;
    }
}
