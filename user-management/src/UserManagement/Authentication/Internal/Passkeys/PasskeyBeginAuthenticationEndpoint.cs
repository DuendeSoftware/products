// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.UserManagement.Authentication.Internal.Passkeys.Results;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.Internal.Passkeys;

internal sealed class PasskeyBeginAuthenticationEndpoint(
    IPasskeyCeremonies ceremonies,
    ILogger<PasskeyBeginAuthenticationEndpoint> logger)
{
    internal async Task<IResult> ProcessDiscoverableAsync(Ct ct)
    {
        logger.PasskeyAuthenticateDiscoverableBeginStarting(LogLevel.Debug);

        var result = await ceremonies.BeginAuthenticationAsync(ct);

        switch (result)
        {
            case PasskeyAuthenticationBeginResult.Success success:
                return new PasskeyBeginResult(success.Session.ChallengeId, success.Session.Options);
            case PasskeyAuthenticationBeginResult.Failure failure:
                logger.PasskeyAuthenticateBeginFailed(LogLevel.Warning, failure.Error);
                return Microsoft.AspNetCore.Http.Results.ValidationProblem(
                    new Dictionary<string, string[]>(),
                    "Unable to authenticate with passkey.");
            default: return Microsoft.AspNetCore.Http.Results.StatusCode((int)HttpStatusCode.InternalServerError);
        }
    }
}
