// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Globalization;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Default claims provider implementation
/// </summary>
public class DefaultClaimsService : IClaimsService
{
    /// <summary>
    /// The logger
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// The user service
    /// </summary>
    protected readonly IProfileService Profile;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultClaimsService"/> class.
    /// </summary>
    /// <param name="profile">The profile service</param>
    /// <param name="logger">The logger</param>
    public DefaultClaimsService(IProfileService profile, ILogger<DefaultClaimsService> logger)
    {
        Logger = logger;
        Profile = profile;
    }

    /// <summary>
    /// Returns claims for an identity token
    /// </summary>
    /// <param name="subject">The subject</param>
    /// <param name="resources">The requested resources</param>
    /// <param name="includeAllIdentityClaims">Specifies if all claims should be included in the token, or if the userinfo endpoint can be used to retrieve them</param>
    /// <param name="request">The raw request</param>
    /// <returns>
    /// Claims for the identity token
    /// </returns>
    public virtual async Task<IEnumerable<Claim>> GetIdentityTokenClaimsAsync(ClaimsPrincipal subject, ResourceValidationResult resources, bool includeAllIdentityClaims, ValidatedRequest request)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultClaimsService.GetIdentityTokenClaims");

        Logger.LogDebug("Getting claims for identity token for subject: {subject} and client: {clientId}",
            subject.GetSubjectId(),
            request.Client.ClientId);

        var outputClaims = new List<Claim>(GetStandardSubjectClaims(subject));
        outputClaims.AddRange(GetOptionalClaims(subject));

        // fetch all identity claims that need to go into the id token
        if (includeAllIdentityClaims || request.Client.AlwaysIncludeUserClaimsInIdToken)
        {
            var additionalClaimTypes = new List<string>();

            foreach (var identityResource in resources.Resources.IdentityResources)
            {
                foreach (var userClaim in identityResource.UserClaims)
                {
                    additionalClaimTypes.Add(userClaim);
                }
            }

            // filter so we don't ask for claim types that we will eventually filter out
            additionalClaimTypes = FilterRequestedClaimTypes(additionalClaimTypes).ToList();

            var context = new ProfileDataRequestContext(
                subject,
                request.Client,
                IdentityServerConstants.ProfileDataCallers.ClaimsProviderIdentityToken,
                additionalClaimTypes)
            {
                RequestedResources = resources,
                ValidatedRequest = request
            };

            await Profile.GetProfileDataAsync(context);

            var claims = FilterProtocolClaims(context.IssuedClaims);
            if (claims != null)
            {
                outputClaims.AddRange(claims);
            }
        }
        else
        {
            Logger.LogDebug("In addition to an id_token, an access_token was requested. No claims other than sub are included in the id_token. To obtain more user claims, either use the user info endpoint or set AlwaysIncludeUserClaimsInIdToken on the client configuration.");
        }

        return outputClaims;
    }

    /// <summary>
    /// Returns claims for an access token.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <param name="resourceResult">The validated resource result</param>
    /// <param name="request">The raw request.</param>
    /// <returns>
    /// Claims for the access token
    /// </returns>
    public virtual async Task<IEnumerable<Claim>> GetAccessTokenClaimsAsync(ClaimsPrincipal subject, ResourceValidationResult resourceResult, ValidatedRequest request)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultClaimsService.GetAccessTokenClaims");

        Logger.LogDebug("Getting claims for access token for client: {clientId}", request.Client.ClientId);

        var outputClaims = new List<Claim>
        {
            new Claim(JwtClaimTypes.ClientId, request.ClientId)
        };

        // log if client ID is overwritten
        if (!string.Equals(request.ClientId, request.Client.ClientId, StringComparison.Ordinal))
        {
            Logger.LogDebug("Client {clientId} is impersonating {impersonatedClientId}", request.Client.ClientId, request.ClientId);
        }

        // check for client claims
        if (request.ClientClaims != null && request.ClientClaims.Count > 0)
        {
            if (subject == null || request.Client.AlwaysSendClientClaims)
            {
                foreach (var claim in request.ClientClaims)
                {
                    var claimType = claim.Type;

                    if (request.Client.ClientClaimsPrefix.IsPresent())
                    {
                        claimType = request.Client.ClientClaimsPrefix + claimType;
                    }

                    outputClaims.Add(new Claim(claimType, claim.Value, claim.ValueType));
                }
            }
        }

        // add scopes (filter offline_access)
        // we use the ScopeValues collection rather than the Resources.Scopes because we support dynamic scope values
        // from the request, so this issues those in the token.
        foreach (var scope in resourceResult.RawScopeValues.Where(x => x != IdentityServerConstants.StandardScopes.OfflineAccess))
        {
            outputClaims.Add(new Claim(JwtClaimTypes.Scope, scope));
        }

        // a user is involved
        if (subject != null)
        {
            if (resourceResult.Resources.OfflineAccess)
            {
                outputClaims.Add(new Claim(JwtClaimTypes.Scope, IdentityServerConstants.StandardScopes.OfflineAccess));
            }

            Logger.LogDebug("Getting claims for access token for subject: {subject}", subject.GetSubjectId());

            outputClaims.AddRange(GetStandardSubjectClaims(subject));
            outputClaims.AddRange(GetOptionalClaims(subject));

            // fetch all resource claims that need to go into the access token
            var additionalClaimTypes = new List<string>();
            foreach (var api in resourceResult.Resources.ApiResources)
            {
                // add claims configured on api resource
                if (api.UserClaims != null)
                {
                    foreach (var claim in api.UserClaims)
                    {
                        additionalClaimTypes.Add(claim);
                    }
                }
            }

            foreach (var scope in resourceResult.Resources.ApiScopes)
            {
                // add claims configured on scopes
                if (scope.UserClaims != null)
                {
                    foreach (var claim in scope.UserClaims)
                    {
                        additionalClaimTypes.Add(claim);
                    }
                }
            }

            // filter so we don't ask for claim types that we will eventually filter out
            additionalClaimTypes = FilterRequestedClaimTypes(additionalClaimTypes).ToList();

            var context = new ProfileDataRequestContext(
                subject,
                request.Client,
                IdentityServerConstants.ProfileDataCallers.ClaimsProviderAccessToken,
                additionalClaimTypes.Distinct())
            {
                RequestedResources = resourceResult,
                ValidatedRequest = request
            };

            await Profile.GetProfileDataAsync(context);

            var claims = FilterProtocolClaims(context.IssuedClaims);
            if (claims != null)
            {
                outputClaims.AddRange(claims);
            }
        }

        return outputClaims;
    }

    /// <summary>
    /// Gets the standard subject claims.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <returns>A list of standard claims</returns>
    protected virtual IEnumerable<Claim> GetStandardSubjectClaims(ClaimsPrincipal subject)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtClaimTypes.Subject, subject.GetSubjectId()),
            new Claim(JwtClaimTypes.AuthenticationTime, subject.GetAuthenticationTimeEpoch().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new Claim(JwtClaimTypes.IdentityProvider, subject.GetIdentityProvider())
        };

        claims.AddRange(subject.GetAuthenticationMethods());

        return claims;
    }

    /// <summary>
    /// Gets additional (and optional) claims from the cookie or incoming subject.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <returns>Additional claims</returns>
    protected virtual IEnumerable<Claim> GetOptionalClaims(ClaimsPrincipal subject)
    {
        var claims = new List<Claim>();

        var acr = subject.FindFirst(JwtClaimTypes.AuthenticationContextClassReference);
        if (acr != null)
        {
            claims.Add(acr);
        }

        return claims;
    }

    /// <summary>
    /// Filters out protocol claims like amr, nonce etc..
    /// </summary>
    /// <param name="claims">The claims.</param>
    /// <returns></returns>
    protected virtual IEnumerable<Claim> FilterProtocolClaims(IEnumerable<Claim> claims)
    {
        var claimsToFilter = claims.Where(x => Constants.Filters.ClaimsServiceFilterClaimTypes.Contains(x.Type));
        if (claimsToFilter.Any())
        {
            var types = claimsToFilter.Select(x => x.Type);
            Logger.LogDebug("Claim types from profile service that were filtered: {claimTypes}", types);
        }
        return claims.Except(claimsToFilter);
    }

    /// <summary>
    /// Filters out protocol claims like amr, nonce etc..
    /// </summary>
    /// <param name="claimTypes">The claim types.</param>
    protected virtual IEnumerable<string> FilterRequestedClaimTypes(IEnumerable<string> claimTypes)
    {
        var claimTypesToFilter = claimTypes.Where(x => Constants.Filters.ClaimsServiceFilterClaimTypes.Contains(x));
        return claimTypes.Except(claimTypesToFilter);
    }
}
