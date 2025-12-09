// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

[ShouldlyMethods]
public static class AssertionExtensions
{
    public static void ShouldBeInvalidProofWithDescription(this DPoPProofValidationResult result, string description)
    {
        result.IsError.ShouldBeTrue();
        result.ErrorDescription.ShouldBe(description);
        result.Error.ShouldBe(OidcConstants.TokenErrors.InvalidDPoPProof);
    }

    public static void ReplayCacheShouldNotBeCalled(this TestDPoPProofValidator validator)
    {
        var mockCache = (TestReplayCache)validator.TestReplayCache;
        mockCache.VerifyAddWasNotCalled();
    }
}
