// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityServer.Configuration.RequestProcessing;
using Duende.IdentityServer.Configuration.Validation.DynamicClientRegistration;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;

/// <summary>
/// Represents the context for a dynamic client registration request, including
/// the original DCR request, the client model that is built up through
/// validation and processing, the caller who made the DCR request, and other
/// contextual information.
/// </summary>
/// <remarks>
/// The context is passed through each step of the DCR validation pipeline
/// (implemented by <see cref="IDynamicClientRegistrationValidator"/>)
/// and the request processing pipeline
/// (implemented by <see cref="IDynamicClientRegistrationRequestProcessor"/>).
/// Validation steps read from <see cref="Request"/> and write validated values onto
/// <see cref="Client"/>. The <see cref="Items"/> dictionary can be used to pass custom
/// state between steps without modifying the core model.
/// </remarks>
public class DynamicClientRegistrationContext
{
    /// <summary>
    /// Initializes a new instance of the <see
    /// cref="DynamicClientRegistrationContext"/> class.
    /// </summary>
    /// <param name="request">The original dynamic client registration request.</param>
    /// <param name="caller">The <see cref="ClaimsPrincipal"/> that made the DCR request.</param>
    public DynamicClientRegistrationContext(DynamicClientRegistrationRequest request, ClaimsPrincipal caller)
    {
        Request = request;
        Caller = caller;
    }

    /// <summary>
    /// The client model that is built up through validation and processing.
    /// </summary>
    /// <remarks>
    /// Each validation step is responsible for reading the corresponding property from
    /// <see cref="Request"/> and writing the validated value onto this client model.
    /// After all validation steps succeed, the request processor finalizes the client
    /// (e.g., assigns a client ID and secret) and persists it via the
    /// <see cref="IClientConfigurationStore"/>.
    /// </remarks>
    public Client Client { get; set; } = new();

    /// <summary>
    /// The original dynamic client registration request.
    /// </summary>
    /// <remarks>
    /// Contains the raw metadata submitted by the client, including grant types, redirect URIs,
    /// scopes, and any IdentityServer-specific extensions. Validation steps read from this
    /// property to populate <see cref="Client"/>.
    /// </remarks>
    public DynamicClientRegistrationRequest Request { get; set; }

    /// <summary>
    /// The <see cref="ClaimsPrincipal"/> that made the DCR request.
    /// </summary>
    /// <remarks>
    /// Represents the authenticated caller of the dynamic client registration endpoint.
    /// Custom validation steps can inspect this principal to apply authorization policies,
    /// for example to restrict which callers may register clients with certain grant types
    /// or scopes.
    /// </remarks>
    public ClaimsPrincipal Caller { get; set; }

    /// <summary>
    /// A collection where additional contextual information may be stored. This
    /// is intended as a place to pass additional custom state between
    /// validation steps.
    /// </summary>
    public Dictionary<string, object> Items { get; set; } = new();
}
