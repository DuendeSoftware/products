// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Events;

/// <summary>
/// Event for failed API authentication
/// </summary>
/// <seealso cref="Event" />
public class ApiAuthenticationFailureEvent : Event
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiAuthenticationFailureEvent"/> class.
    /// </summary>
    /// <param name="apiName">Name of the API.</param>
    /// <param name="message">The message.</param>
    public ApiAuthenticationFailureEvent(string apiName, string message)
        : base(EventCategories.Authentication,
            "API Authentication Failure",
            EventTypes.Failure,
            EventIds.ApiAuthenticationFailure,
            message) => ApiName = apiName;

    /// <summary>
    /// Gets or sets the name of the API.
    /// </summary>
    /// <value>
    /// The name of the API.
    /// </value>
    public string ApiName { get; set; }
}
