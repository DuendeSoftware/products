// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AspNetCore.Authentication.JwtBearer.TestFramework;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

public class ReplayTests : DPoPProofValidatorTestBase
{
    [Fact]
    public async Task replays_detected_in_ValidateReplay_fail()
    {
        Options.EnableReplayDetection = true;
        ReplayCache.ExistsFunc = jti => jti == TokenIdHash;
        Result.TokenIdHash = TokenIdHash;

        await ProofValidator.ValidateReplay(Context, Result);

        Result.ShouldBeInvalidProofWithDescription("Detected DPoP proof token replay.");
    }

    [Theory]
    [InlineData(true, false, ClockSkew, 0)]
    [InlineData(false, true, 0, ClockSkew)]
    [InlineData(true, true, ClockSkew, ClockSkew * 2)]
    [InlineData(true, true, ClockSkew * 2, ClockSkew)]
    [InlineData(true, true, ClockSkew * 2, ClockSkew * 2)]
    public async Task new_proof_tokens_are_added_to_replay_cache(bool validateIat, bool validateNonce, int clientClockSkew, int serverClockSkew)
    {
        ReplayCache.ExistsFunc = _ => false;

        Options.ProofTokenExpirationMode = (validateIat && validateNonce) ? DPoPProofExpirationMode.Both
            : validateIat ? DPoPProofExpirationMode.IssuedAt : DPoPProofExpirationMode.Nonce;
        Options.ProofTokenIssuedAtClockSkew = TimeSpan.FromSeconds(clientClockSkew);
        Options.ProofTokenNonceClockSkew = TimeSpan.FromSeconds(serverClockSkew);
        Options.ProofTokenLifetime = TimeSpan.FromSeconds(ValidFor);
        Options.EnableReplayDetection = true;

        Result.TokenIdHash = TokenIdHash;

        await ProofValidator.ValidateReplay(Context, Result);

        Result.IsError.ShouldBeFalse();
        var skew = validateIat && validateNonce
            ? Math.Max(clientClockSkew, serverClockSkew)
            : (validateIat ? clientClockSkew : serverClockSkew);
        var expectedExpiration = TimeSpan.FromSeconds(skew * 2)
            .Add(TimeSpan.FromSeconds(ValidFor));
        ReplayCache.ShouldHaveAdded(TokenIdHash, expectedExpiration);
    }
}
