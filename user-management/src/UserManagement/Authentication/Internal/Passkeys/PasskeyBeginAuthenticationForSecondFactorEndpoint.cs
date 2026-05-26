// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Internal.Passkeys.Results;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.Internal.Passkeys;

internal sealed class PasskeyBeginAuthenticationForSecondFactorEndpoint(
    IPasskeyCeremonies ceremonies,
    ISecondFactorPasskeyAuthenticationResolver resolver,
    ILogger<PasskeyBeginAuthenticationForSecondFactorEndpoint> logger)
{
    internal async Task<IResult> ProcessAsync(Ct ct)
    {
        logger.PasskeyAuthenticateBeginStarting(LogLevel.Debug);

        var userSubjectId = await resolver.ResolveAsync(ct);

        if (userSubjectId is null)
        {
            return Microsoft.AspNetCore.Http.Results.ValidationProblem(new Dictionary<string, string[]>(), "Unable to resolve user for second-factor passkey authentication.");
        }

        var result = await ceremonies.BeginAuthenticationAsync(
            userSubjectId.Value,
            ct);

        switch (result)
        {
            case PasskeyAuthenticationBeginResult.Success success:
                return new PasskeyBeginResult(success.Session.ChallengeId, success.Session.Options);
            case PasskeyAuthenticationBeginResult.Failure failure:
                logger.PasskeyAuthenticateBeginFailed(LogLevel.Warning, failure.Error);
                return Microsoft.AspNetCore.Http.Results.ValidationProblem(new Dictionary<string, string[]>(), "Unable to authenticate with passkey.");
            default:
                throw new InvalidOperationException("Unexpected result type from BeginAuthenticationAsync.");
        }
    }
}
