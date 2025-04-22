// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Context needed to validate a pushed authorization request.
/// </summary>
public class PushedAuthorizationRequestValidationContext
{
    /// <summary>
    /// Initializes an instance of the <see cref="PushedAuthorizationRequestValidationContext"/> class.
    /// </summary>
    /// <param name="requestParameters">The raw parameters that were passed to the PAR endpoint.</param>
    /// <param name="client">The client that made the request.</param>
    public PushedAuthorizationRequestValidationContext(NameValueCollection requestParameters, Client client)
    {
        RequestParameters = requestParameters;
        Client = client;
    }

    /// <summary>
    /// The request form parameters
    /// </summary>
    public NameValueCollection RequestParameters { get; set; }

    /// <summary>
    /// The validation result of client authentication
    /// </summary>
    public Client Client { get; set; }

    /// <summary>
    /// The DPoP proof token sent to the endpoint, if any
    /// </summary>
    public string DPoPProofToken { get; set; }
}
