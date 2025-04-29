// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Diagnostics;
using System.Security.Claims;
using System.Security.Principal;
using Duende.IdentityModel;

namespace Duende.IdentityServer.Extensions;

/// <summary>
/// Extension methods for <see cref="System.Security.Principal.IPrincipal"/> and <see cref="System.Security.Principal.IIdentity"/> .
/// </summary>
public static class PrincipalExtensions
{
    /// <summary>
    /// Gets the authentication time.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static DateTime GetAuthenticationTime(this IPrincipal principal) => DateTimeOffset.FromUnixTimeSeconds(principal.GetAuthenticationTimeEpoch()).UtcDateTime;

    /// <summary>
    /// Gets the authentication epoch time.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static long GetAuthenticationTimeEpoch(this IPrincipal principal) => principal.Identity.GetAuthenticationTimeEpoch();

    /// <summary>
    /// Gets the authentication epoch time.
    /// </summary>
    /// <param name="identity">The identity.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static long GetAuthenticationTimeEpoch(this IIdentity identity)
    {
        var id = (ClaimsIdentity)identity;
        var claim = id.FindFirst(JwtClaimTypes.AuthenticationTime);

        if (claim == null)
        {
            throw new InvalidOperationException("auth_time is missing.");
        }

        return long.Parse(claim.Value);
    }

    /// <summary>
    /// Gets the subject identifier.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static string GetSubjectId(this IPrincipal principal) => principal.Identity.GetSubjectId();

    /// <summary>
    /// Gets the subject identifier.
    /// </summary>
    /// <param name="identity">The identity.</param>
    /// <returns></returns>
    /// <exception cref="System.InvalidOperationException">sub claim is missing</exception>
    [DebuggerStepThrough]
    public static string GetSubjectId(this IIdentity identity)
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
    /// Gets the name.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static string GetDisplayName(this ClaimsPrincipal principal)
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
    /// Gets the authentication method.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static string GetAuthenticationMethod(this IPrincipal principal) => principal.Identity.GetAuthenticationMethod();

    /// <summary>
    /// Gets the authentication method claims.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static IEnumerable<Claim> GetAuthenticationMethods(this IPrincipal principal) => principal.Identity.GetAuthenticationMethods();

    /// <summary>
    /// Gets the authentication method.
    /// </summary>
    /// <param name="identity">The identity.</param>
    /// <returns></returns>
    /// <exception cref="System.InvalidOperationException">amr claim is missing</exception>
    [DebuggerStepThrough]
    public static string GetAuthenticationMethod(this IIdentity identity)
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
    /// <param name="identity">The identity.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static IEnumerable<Claim> GetAuthenticationMethods(this IIdentity identity)
    {
        var id = identity as ClaimsIdentity;
        return id.FindAll(JwtClaimTypes.AuthenticationMethod);
    }

    /// <summary>
    /// Gets the identity provider.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static string GetIdentityProvider(this IPrincipal principal) => principal.Identity.GetIdentityProvider();

    /// <summary>
    /// Gets the identity provider.
    /// </summary>
    /// <param name="identity">The identity.</param>
    /// <returns></returns>
    /// <exception cref="System.InvalidOperationException">idp claim is missing</exception>
    [DebuggerStepThrough]
    public static string GetIdentityProvider(this IIdentity identity)
    {
        var id = identity as ClaimsIdentity;
        var claim = id.FindFirst(JwtClaimTypes.IdentityProvider);

        if (claim == null)
        {
            throw new InvalidOperationException("idp claim is missing");
        }

        return claim.Value;
    }

    /// <summary>
    /// Gets the tenant.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns></returns>
    [DebuggerStepThrough]
    public static string GetTenant(this ClaimsPrincipal principal) => principal.FindFirst(IdentityServerConstants.ClaimTypes.Tenant)?.Value;

    /// <summary>
    /// Determines whether this instance is authenticated.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns>
    ///   <c>true</c> if the specified principal is authenticated; otherwise, <c>false</c>.
    /// </returns>
    [DebuggerStepThrough]
    public static bool IsAuthenticated(this IPrincipal principal) => principal != null && principal.Identity != null && principal.Identity.IsAuthenticated;
}
