// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Validation;
using Duende.IdentityServer.Extensions;

namespace Duende.IdentityServer.Events;

/// <summary>
/// Event for device authorization failure
/// </summary>
/// <seealso cref="Event" />
public class DeviceAuthorizationFailureEvent : Event
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceAuthorizationFailureEvent"/> class.
    /// </summary>
    /// <param name="result">The result.</param>
    public DeviceAuthorizationFailureEvent(DeviceAuthorizationRequestValidationResult result)
        : this()
    {
        if (result.ValidatedRequest != null)
        {
            ClientId = result.ValidatedRequest.Client?.ClientId;
            ClientName = result.ValidatedRequest.Client?.ClientName;
            Scopes = result.ValidatedRequest.RequestedScopes?.ToSpaceSeparatedString();
                
        }

        Endpoint = IdentityServerConstants.EndpointNames.DeviceAuthorization;
        Error = result.Error;
        ErrorDescription = result.ErrorDescription;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceAuthorizationFailureEvent"/> class.
    /// </summary>
    public DeviceAuthorizationFailureEvent()
        : base(EventCategories.DeviceFlow,
            "Device Authorization Failure",
            EventTypes.Failure,
            EventIds.DeviceAuthorizationFailure)
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
    /// Gets or sets the scopes.
    /// </summary>
    /// <value>
    /// The scopes.
    /// </value>
    public string Scopes { get; set; }
        
    /// <summary>
    /// Gets or sets the error.
    /// </summary>
    /// <value>
    /// The error.
    /// </value>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the error description.
    /// </summary>
    /// <value>
    /// The error description.
    /// </value>
    public string ErrorDescription { get; set; }
}