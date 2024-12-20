// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Extensions;
using System;
using System.Security.Claims;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Context describing the is-active check
/// </summary>
public class IsActiveContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IsActiveContext"/> class.
    /// </summary>
    public IsActiveContext(ClaimsPrincipal subject, Client client, string caller)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(caller);

        Subject = subject;
        Client = client;
        Caller = caller;
            
        IsActive = true;
    }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    /// <value>
    /// The subject.
    /// </value>
    public ClaimsPrincipal Subject { get; set; }

    /// <summary>
    /// Gets or sets the client.
    /// </summary>
    /// <value>
    /// The client.
    /// </value>
    public Client Client { get; set; }

    /// <summary>
    /// Gets or sets the caller.
    /// </summary>
    /// <value>
    /// The caller.
    /// </value>
    public string Caller { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the subject is active and can receive tokens.
    /// </summary>
    /// <value>
    ///   <c>true</c> if the subject is active; otherwise, <c>false</c>.
    /// </value>
    public bool IsActive { get; set; }
}
