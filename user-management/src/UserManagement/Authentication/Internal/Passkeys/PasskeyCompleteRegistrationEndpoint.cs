// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.UserManagement.Authentication.Internal.Passkeys.Results;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Duende.UserManagement.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.Internal.Passkeys;

internal sealed class PasskeyCompleteRegistrationEndpoint(
    IPasskeyCeremonies ceremonies,
    IUserAuthenticatorsSelfService selfService,
    ILogger<PasskeyCompleteRegistrationEndpoint> logger)
{
    internal async Task<IResult> ProcessAsync(
        HttpContext context,
        PasskeyCompleteRegistrationRequest request,
        Ct ct)
    {
        var subjectIdClaim = context.User.FindFirst(JwtClaimTypes.Subject)?.Value
                             ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(subjectIdClaim))
        {
            logger.PasskeyRegisterCompleteUnauthenticated(LogLevel.Warning);
            return Microsoft.AspNetCore.Http.Results.StatusCode((int)HttpStatusCode.Unauthorized);
        }

        var userSubjectId = UserSubjectId.Create(subjectIdClaim);
        using var scope = logger.BeginSubjectScope(userSubjectId);
        var result = await ceremonies.CompleteRegistrationAsync(request, ct);

        switch (result)
        {
            case PasskeyRegistrationCompleteResult.Success success:
                if (await selfService.TryAddPasskeyAsync(userSubjectId, success.Credential, ct))
                {
                    return new PasskeyCompleteRegistrationResult(success.Credential.CredentialId.ToString());
                }

                logger.PasskeyRegisterCompletePersistFailed(LogLevel.Error);
                return Microsoft.AspNetCore.Http.Results.StatusCode((int)HttpStatusCode.InternalServerError);

            case PasskeyRegistrationCompleteResult.Failure failure:
                logger.PasskeyRegisterCompleteFailed(LogLevel.Warning, failure.Error);
                return Microsoft.AspNetCore.Http.Results.ValidationProblem(
                    new Dictionary<string, string[]>(),
                    "Unable to complete passkey registration.");

            default:
                return Microsoft.AspNetCore.Http.Results.StatusCode((int)HttpStatusCode.InternalServerError);
        }
    }
}
