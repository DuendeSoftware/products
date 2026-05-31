// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.Internal.Passkeys;

internal sealed class PasskeyCompleteAuthenticationEndpoint(
    IPasskeyCeremonies ceremonies,
    IUserAuthenticatorsSelfService selfService,
    IPasskeySignInHandler signInHandler,
    ILogger<PasskeyCompleteAuthenticationEndpoint> logger)
{
    internal async Task<IResult> ProcessAsync(
        HttpContext context,
        PasskeyCompleteAuthenticationRequest request,
        Ct ct)
    {
        var result = await ceremonies.CompleteAuthenticationAsync(request, ct);

        if (result is not PasskeyAuthenticationCompleteResult.Success success)
        {
            if (result is PasskeyAuthenticationCompleteResult.Failure failure)
            {
                logger.PasskeyAuthenticateCompleteFailed(LogLevel.Warning, failure.Error);
            }
            return Microsoft.AspNetCore.Http.Results.ValidationProblem(new Dictionary<string, string[]>(), "Unable to authenticate with passkey.");
        }

        var user = await selfService.TryGetAsync(success.UserSubjectId, ct);
        if (user is null)
        {
            logger.PasskeyAuthenticateCompleteUserNotFound(LogLevel.Warning, success.UserSubjectId.ToString());
            return Microsoft.AspNetCore.Http.Results.ValidationProblem(
                new Dictionary<string, string[]>(),
                "Unable to authenticate with passkey.");
        }

        var signInResult = await signInHandler.SignInAsync(context, user, success.UserVerified, success.BackedUp, ct);

        logger.PasskeyAuthenticateCompleteSignedIn(LogLevel.Information, user.SubjectId.Value);

        return signInResult;
    }
}
