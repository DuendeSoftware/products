// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;

namespace MfaStepUp;

internal sealed class MaxAgeAuthorizationHandler(TimeProvider timeProvider) :
    AuthorizationHandler<MaxAgeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, MaxAgeRequirement requirement)
    {
        var mfaTimeClaim = context.User.FindFirst(MfaAuthScheme.MfaTimeClaimType)?.Value;
        if (mfaTimeClaim is null || !long.TryParse(mfaTimeClaim, out var mfaTimeSeconds))
        {
            return Task.CompletedTask;
        }

        if (timeProvider.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(mfaTimeSeconds) < requirement.MaxAge)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
