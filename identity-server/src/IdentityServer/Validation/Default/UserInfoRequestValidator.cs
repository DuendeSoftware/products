// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Default userinfo request validator
/// </summary>
/// <seealso cref="IUserInfoRequestValidator" />
internal class UserInfoRequestValidator : IUserInfoRequestValidator
{
    private readonly ITokenValidator _tokenValidator;
    private readonly IProfileService _profile;
    private readonly ILogger _logger;
    private readonly IServerSideTicketStore _serverSideTicketStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserInfoRequestValidator" /> class.
    /// </summary>
    /// <param name="tokenValidator">The token validator.</param>
    /// <param name="profile">The profile service</param>
    /// <param name="logger">The logger.</param>
    /// <param name="serverSideTicketStore"></param>
    public UserInfoRequestValidator(
        ITokenValidator tokenValidator,
        IProfileService profile,
        ILogger<UserInfoRequestValidator> logger,
        IServerSideTicketStore serverSideTicketStore = null)
    {
        _tokenValidator = tokenValidator;
        _profile = profile;
        _logger = logger;
        _serverSideTicketStore = serverSideTicketStore;
    }

    /// <summary>
    /// Validates a userinfo request.
    /// </summary>
    /// <param name="accessToken">The access token.</param>
    /// <returns></returns>
    /// <exception cref="System.NotImplementedException"></exception>
    public async Task<UserInfoRequestValidationResult> ValidateRequestAsync(string accessToken)
    {
        using var activity = Tracing.BasicActivitySource.StartActivity("UserInfoRequestValidator.ValidateRequest");

        // the access token needs to be valid and have at least the openid scope
        var tokenResult = await _tokenValidator.ValidateAccessTokenAsync(
            accessToken,
            IdentityServerConstants.StandardScopes.OpenId);

        if (tokenResult.IsError)
        {
            return new UserInfoRequestValidationResult
            {
                IsError = true,
                Error = tokenResult.Error
            };
        }

        // the token must have a one sub claim
        var subClaim = tokenResult.Claims?.SingleOrDefault(c => c.Type == JwtClaimTypes.Subject);
        if (subClaim == null)
        {
            _logger.LogError("Token contains no sub claim");

            return new UserInfoRequestValidationResult
            {
                IsError = true,
                Error = OidcConstants.ProtectedResourceErrors.InvalidToken
            };
        }

        // create subject for the incoming access token
        ClaimsPrincipal subject = null;

        if (_serverSideTicketStore != null)
        {
            // we are using server-side sessions, so let's fetch the user from the session store
            // this makes the Subject in the profile service consistent with all other calls into the profile service
            var sid = tokenResult.Claims.SingleOrDefault(x => x.Type == JwtClaimTypes.SessionId)?.Value;
            if (sid != null)
            {
                var sessions = await _serverSideTicketStore.GetSessionsAsync(new SessionFilter
                {
                    SubjectId = subClaim.Value,
                    SessionId = sid,
                });

                if (sessions.Count == 1)
                {
                    _logger.LogDebug("Loading subject claims from server-side session store");
                    subject = sessions.First().AuthenticationTicket.Principal;
                }
            }
        }

        if (subject == null)
        {
            _logger.LogDebug("Loading subject claims from access token");
            // this falls back to prior behavior which provides the best we can for the subject based on claims from the access token
            var claims = tokenResult.Claims.Where(x => !Constants.Filters.ProtocolClaimsFilter.Contains(x.Type));
            subject = Principal.Create("UserInfo", claims.ToArray());
        }

        // make sure user is still active
        var isActiveContext = new IsActiveContext(subject, tokenResult.Client!, IdentityServerConstants.ProfileIsActiveCallers.UserInfoRequestValidation);
        await _profile.IsActiveAsync(isActiveContext);

        if (isActiveContext.IsActive == false)
        {
            _logger.LogError("User is not active: {sub}", subject.GetSubjectId());

            return new UserInfoRequestValidationResult
            {
                IsError = true,
                Error = OidcConstants.ProtectedResourceErrors.InvalidToken
            };
        }

        return new UserInfoRequestValidationResult
        {
            IsError = false,
            TokenValidationResult = tokenResult,
            Subject = subject
        };
    }
}
