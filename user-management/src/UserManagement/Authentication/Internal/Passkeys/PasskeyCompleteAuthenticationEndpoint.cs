// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.UserManagement.Authentication.Internal.Passkeys.Results;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Authentication.Internal.Passkeys;

internal sealed class PasskeyCompleteAuthenticationEndpoint(
    IPasskeyCeremonies ceremonies,
    IUserAuthenticatorsSelfService selfService,
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

        var email = user.OtpAddresses
            .FirstOrDefault(a => a.Channel == OtpChannel.Email)
            ?.SubjectId.ToString();

        var subjectIdString = user.SubjectId.Value;
        var displayName = user.UserName?.ToString() ?? email;

        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.Subject, subjectIdString),
            new Claim(JwtClaimTypes.AuthenticationMethod, "passkey"),
            new Claim(JwtClaimTypes.AuthenticationTime, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
        };

        if (displayName is not null)
        {
            claims.Add(new Claim(JwtClaimTypes.Name, displayName));
        }

        if (email is not null)
        {
            claims.Add(new Claim(JwtClaimTypes.Email, email));
        }

        var identity = new ClaimsIdentity(claims, "Duende.IdentityServer", JwtClaimTypes.Name, JwtClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8),
            IssuedUtc = DateTimeOffset.UtcNow,
            AllowRefresh = true
        };

        await context.SignInAsync(principal, authProperties);

        logger.PasskeyAuthenticateCompleteSignedIn(LogLevel.Information, subjectIdString);

        return new PasskeyCompleteAuthenticationResult(success.UserVerified, success.BackedUp);
    }
}
