// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer.Extensions;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Extensions for ProfileDataRequestContext
/// </summary>
public static class ProfileDataRequestContextExtensions
{
    extension(ProfileDataRequestContext context)
    {
        /// <summary>
        /// Filters the claims based on requested claim types.
        /// </summary>
        /// <param name="claims">The claims.</param>
        /// <returns></returns>
        public List<Claim> FilterClaims(IEnumerable<Claim> claims)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(claims);

            return claims.Where(x => context.RequestedClaimTypes.Contains(x.Type)).ToList();
        }

        /// <summary>
        /// Filters the claims based on the requested claim types and then adds them to the IssuedClaims collection.
        /// </summary>
        /// <param name="claims">The claims.</param>
        public void AddRequestedClaims(IEnumerable<Claim> claims)
        {
            if (context.RequestedClaimTypes.Any())
            {
                context.IssuedClaims.AddRange(context.FilterClaims(claims));
            }
        }

        /// <summary>
        /// Logs the profile request.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public void LogProfileRequest(ILogger logger) => logger.LogDebug("Get profile called for subject {subject} from application {application} with claim types {claimTypes} via {caller}",
                context.Subject.GetSubjectId(),
                context.Application?.DisplayName ?? context.Application?.Identifier,
                context.RequestedClaimTypes,
                context.Caller);

        /// <summary>
        /// Logs the issued claims.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public void LogIssuedClaims(ILogger logger) => logger.LogDebug("Issued claims: {claims}", context.IssuedClaims.Select(c => c.Type));
    }
}
