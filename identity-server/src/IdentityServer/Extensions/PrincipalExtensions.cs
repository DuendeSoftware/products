// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Security.Principal;
using Duende.IdentityModel;

namespace Duende.IdentityServer.Extensions;

/// <summary>
/// Extension methods for <see cref="System.Security.Principal.IPrincipal"/> and <see cref="System.Security.Principal.IIdentity"/> .
/// </summary>
public static class PrincipalExtensions
{
    extension(IPrincipal principal)
    {
        /// <summary>
        /// Gets the authentication time.
        /// </summary>
        /// <returns></returns>
        [DebuggerStepThrough]
        public DateTime GetAuthenticationTime() => DateTimeOffset.FromUnixTimeSeconds(principal.GetAuthenticationTimeEpoch()).UtcDateTime;

        /// <summary>
        /// Gets the authentication epoch time.
        /// </summary>
        /// <returns></returns>
        [DebuggerStepThrough]
        public long GetAuthenticationTimeEpoch() => principal.Identity.GetAuthenticationTimeEpoch();

        /// <summary>
        /// Gets the subject identifier.
        /// </summary>
        /// <returns></returns>
        [DebuggerStepThrough]
        public string GetSubjectId() => principal.Identity.GetSubjectId();

        /// <summary>
        /// Gets the authentication method.
        /// </summary>
        /// <returns></returns>
        [DebuggerStepThrough]
        public string GetAuthenticationMethod() => principal.Identity.GetAuthenticationMethod();

        /// <summary>
        /// Gets the authentication method claims.
        /// </summary>
        /// <returns></returns>
        [DebuggerStepThrough]
        public IEnumerable<Claim> GetAuthenticationMethods() => principal.Identity.GetAuthenticationMethods();

        /// <summary>
        /// Gets the identity provider.
        /// </summary>
        /// <returns></returns>
        [DebuggerStepThrough]
        public string GetIdentityProvider() => principal.Identity.GetIdentityProvider();

        /// <summary>
        /// Determines whether this instance is authenticated.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if the specified principal is authenticated; otherwise, <c>false</c>.
        /// </returns>
        [DebuggerStepThrough]
        public bool IsAuthenticated() => principal != null && principal.Identity != null && principal.Identity.IsAuthenticated;
    }

    extension(IIdentity identity)
    {
        /// <summary>
        /// Gets the authentication epoch time.
        /// </summary>
        /// <returns></returns>
        [DebuggerStepThrough]
        public long GetAuthenticationTimeEpoch()
        {
            var id = (ClaimsIdentity)identity;
            var claim = id.FindFirst(JwtClaimTypes.AuthenticationTime);

            if (claim == null)
            {
                throw new InvalidOperationException("auth_time is missing.");
            }

            return long.Parse(claim.Value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets the subject identifier.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException">sub claim is missing</exception>
        [DebuggerStepThrough]
        public string GetSubjectId()
        {
            var id = identity as ClaimsIdentity;
            var claim = id.FindFirst(JwtClaimTypes.Subject);

            if (claim == null)
            {
                throw new InvalidOperationException("sub claim is missing");
            }

            return claim.Value;
        }

        /// <summary>
        /// Gets the authentication method.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException">amr claim is missing</exception>
        [DebuggerStepThrough]
        public string GetAuthenticationMethod()
        {
            var id = identity as ClaimsIdentity;
            var claim = id.FindFirst(JwtClaimTypes.AuthenticationMethod);

            if (claim == null)
            {
                throw new InvalidOperationException("amr claim is missing");
            }

            return claim.Value;
        }

        /// <summary>
        /// Gets the authentication method claims.
        /// </summary>
        /// <returns></returns>
        [DebuggerStepThrough]
        public IEnumerable<Claim> GetAuthenticationMethods()
        {
            var id = identity as ClaimsIdentity;
            return id.FindAll(JwtClaimTypes.AuthenticationMethod);
        }

        /// <summary>
        /// Gets the identity provider.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException">idp claim is missing</exception>
        [DebuggerStepThrough]
        public string GetIdentityProvider()
        {
            var id = identity as ClaimsIdentity;
            var claim = id.FindFirst(JwtClaimTypes.IdentityProvider);

            if (claim == null)
            {
                throw new InvalidOperationException("idp claim is missing");
            }

            return claim.Value;
        }
    }

    extension(ClaimsPrincipal principal)
    {
        /// <summary>
        /// Gets the name.
        /// </summary>
        /// <returns></returns>
        [DebuggerStepThrough]
        public string GetDisplayName()
        {
            var name = principal.Identity.Name;
            if (name.IsPresent())
            {
                return name;
            }

            var sub = principal.FindFirst(JwtClaimTypes.Subject);
            if (sub != null)
            {
                return sub.Value;
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets the tenant.
        /// </summary>
        /// <returns></returns>
        [DebuggerStepThrough]
        public string GetTenant() => principal.FindFirst(IdentityServerConstants.ClaimTypes.Tenant)?.Value;
    }
}
