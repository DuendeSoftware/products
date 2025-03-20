// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Hosting.LocalApiAuthentication;

/// <summary>
/// Events for local API authentication
/// </summary>
public class LocalApiAuthenticationEvents
{
    /// <summary>
    /// Invoked after the security token has passed validation and a ClaimsIdentity has been generated.
    /// </summary>
    public Func<ClaimsTransformationContext, Task> OnClaimsTransformation { get; set; } = context => Task.CompletedTask;

    /// <summary>
    /// Invoked after the security token has passed validation and a ClaimsIdentity has been generated.
    /// </summary>
    public virtual Task ClaimsTransformation(ClaimsTransformationContext context) => OnClaimsTransformation(context);

}

/// <summary>
/// Context class for local API claims transformation
/// </summary>
public class ClaimsTransformationContext
{
    /// <summary>
    /// The principal
    /// </summary>
    public ClaimsPrincipal Principal { get; set; } = null!;

    /// <summary>
    /// the HTTP context
    /// </summary>
    public HttpContext HttpContext { get; internal set; } = null!;
}
