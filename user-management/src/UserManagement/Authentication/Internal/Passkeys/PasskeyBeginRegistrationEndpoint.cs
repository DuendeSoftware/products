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

internal sealed class PasskeyBeginRegistrationEndpoint(
    IPasskeyCeremonies ceremonies,
    ILogger<PasskeyBeginRegistrationEndpoint> logger)
{
    internal async Task<IResult> ProcessAsync(HttpContext context, Ct ct)
    {
        var subjectIdClaim = context.User.FindFirst(JwtClaimTypes.Subject)?.Value
                             ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(subjectIdClaim))
        {
            logger.PasskeyRegisterBeginUnauthenticated(LogLevel.Warning);
            return Microsoft.AspNetCore.Http.Results.StatusCode((int)HttpStatusCode.Unauthorized);
        }

        var userSubjectId = UserSubjectId.Create(subjectIdClaim);
        using var scope = logger.BeginSubjectScope(userSubjectId);

        var userName = context.User.FindFirst(JwtClaimTypes.Email)?.Value
                       ?? context.User.FindFirst(JwtClaimTypes.Name)?.Value
                       ?? "user";
        var displayName = context.User.FindFirst(JwtClaimTypes.Name)?.Value ?? userName;

        var session = await ceremonies.BeginRegistrationAsync(userSubjectId, userName, displayName, ct);

        return new PasskeyBeginResult(session.ChallengeId, session.Options);
    }
}
