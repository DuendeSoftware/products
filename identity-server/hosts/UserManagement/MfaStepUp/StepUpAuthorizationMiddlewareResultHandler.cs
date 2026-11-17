// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.UserManagement.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using SamplesShared;

namespace MfaStepUp;

internal sealed class StepUpAuthorizationMiddlewareResultHandler(
    IUserAuthenticatorsSelfService selfService, ILogger<StepUpAuthorizationMiddlewareResultHandler> logger)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Forbidden)
        {
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var failures = authorizeResult.AuthorizationFailure!.FailedRequirements.ToList();
        var mfaRequirement = failures.OfType<ClaimsAuthorizationRequirement>()
            .FirstOrDefault(r =>
                r.ClaimType == JwtClaimTypes.AuthenticationMethod &&
                r.AllowedValues!.Contains(OidcConstants.AuthenticationMethods.MultiFactorAuthentication));

        var maxAgeRequirement = failures.OfType<MaxAgeRequirement>().FirstOrDefault();

        if (mfaRequirement is null && maxAgeRequirement is null)
        {
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        if (await selfService.TryGetAsync(context.User.GetSubjectId(), context.RequestAborted)
            is not { } authenticators)
        {
            logger.LogTrace("MFA required but failed to get authenticators");
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        if (authenticators.Passkeys.Count == 0)
        {
            logger.LogTrace("MFA required but user has no passkeys registered");
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        await context.ChallengeAsync(MfaAuthScheme.Name);
    }
}
