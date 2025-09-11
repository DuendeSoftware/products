// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Class describing the profile data request
/// </summary>
public class ProfileDataRequestContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileDataRequestContext"/> class.
    /// </summary>
    public ProfileDataRequestContext()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileDataRequestContext" /> class.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <param name="client">The client.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="requestedClaimTypes">The requested claim types.</param>
    public ProfileDataRequestContext(ClaimsPrincipal subject, Client client, string caller, IEnumerable<string> requestedClaimTypes)
    {
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        Client = client ?? throw new ArgumentNullException(nameof(client));
        Caller = caller ?? throw new ArgumentNullException(nameof(caller));
        RequestedClaimTypes = requestedClaimTypes ?? throw new ArgumentNullException(nameof(requestedClaimTypes));
    }

    /// <summary>
    /// Gets or sets the validatedRequest.
    /// </summary>
    /// <value>
    /// The validatedRequest.
    /// </value>
    public ValidatedRequest ValidatedRequest { get; set; } = default!;

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    /// <value>
    /// The subject.
    /// </value>
    public ClaimsPrincipal Subject { get; set; } = default!;

    /// <summary>
    /// Gets or sets the requested claim types.
    /// </summary>
    /// <value>
    /// The requested claim types.
    /// </value>
    public IEnumerable<string> RequestedClaimTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the client id.
    /// </summary>
    /// <value>
    /// The client id.
    /// </value>
    public Client Client { get; set; } = default!;

    /// <summary>
    /// Gets or sets the caller.
    /// </summary>
    /// <value>
    /// The caller.
    /// </value>
    public string Caller { get; set; } = default!;

    /// <summary>
    /// Gets or sets the requested resources (if available).
    /// </summary>
    /// <value>
    /// The resources.
    /// </value>
    public ResourceValidationResult RequestedResources { get; set; } = default!;

    /// <summary>
    /// Gets or sets the issued claims.
    /// </summary>
    /// <value>
    /// The issued claims.
    /// </value>
    public List<Claim> IssuedClaims { get; set; } = [];
}
