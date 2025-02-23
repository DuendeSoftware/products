// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Events;

/// <summary>
/// Event for successful backchannel authentication result
/// </summary>
/// <seealso cref="Event" />
public class BackchannelAuthenticationSuccessEvent : Event
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BackchannelAuthenticationSuccessEvent"/> class.
    /// </summary>
    /// <param name="request">The request.</param>
    public BackchannelAuthenticationSuccessEvent(BackchannelAuthenticationRequestValidationResult request)
        : this()
    {
        ClientId = request.ValidatedRequest.Client.ClientId;
        ClientName = request.ValidatedRequest.Client.ClientName;
        Endpoint = IdentityServerConstants.EndpointNames.BackchannelAuthentication;
        SubjectId = request.ValidatedRequest.Subject?.GetSubjectId();
        Scopes = request.ValidatedRequest.ValidatedResources.RawScopeValues.ToSpaceSeparatedString();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BackchannelAuthenticationSuccessEvent"/> class.
    /// </summary>
    protected BackchannelAuthenticationSuccessEvent()
        : base(EventCategories.BackchannelAuthentication,
            "Backchannel Authentication Success",
            EventTypes.Success,
            EventIds.BackchannelAuthenticationSuccess)
    {
    }

    /// <summary>
    /// Gets or sets the client identifier.
    /// </summary>
    /// <value>
    /// The client identifier.
    /// </value>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the name of the client.
    /// </summary>
    /// <value>
    /// The name of the client.
    /// </value>
    public string ClientName { get; set; }

    /// <summary>
    /// Gets or sets the endpoint.
    /// </summary>
    /// <value>
    /// The endpoint.
    /// </value>
    public string Endpoint { get; set; }

    /// <summary>
    /// Gets or sets the subject identifier.
    /// </summary>
    /// <value>
    /// The subject identifier.
    /// </value>
    public string SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the scopes.
    /// </summary>
    /// <value>
    /// The scopes.
    /// </value>
    public string Scopes { get; set; }
}