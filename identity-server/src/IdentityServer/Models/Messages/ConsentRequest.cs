// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Specialized;
using System.Security.Cryptography;
using System.Text;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Models the parameters to identify a request for consent.
/// </summary>
public class ConsentRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentRequest"/> class.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="subject">The subject.</param>
    public ConsentRequest(AuthorizationRequest request, string subject)
    {
        ClientId = request.Client.ClientId;
        Nonce = request.Parameters[OidcConstants.AuthorizeRequest.Nonce];
        ScopesRequested = request.Parameters[OidcConstants.AuthorizeRequest.Scope].ParseScopesString();
        Subject = subject;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentRequest"/> class.
    /// </summary>
    /// <param name="parameters">The parameters.</param>
    /// <param name="subject">The subject.</param>
    public ConsentRequest(NameValueCollection parameters, string subject)
    {
        ClientId = parameters[OidcConstants.AuthorizeRequest.ClientId];
        Nonce = parameters[OidcConstants.AuthorizeRequest.Nonce];
        ScopesRequested = parameters[OidcConstants.AuthorizeRequest.Scope].ParseScopesString();
        Subject = subject;
    }

    /// <summary>
    /// Gets or sets the client identifier.
    /// </summary>
    /// <value>
    /// The client identifier.
    /// </value>
    public string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the nonce.
    /// </summary>
    /// <value>
    /// The nonce.
    /// </value>
    public string Nonce { get; set; }

    /// <summary>
    /// Gets or sets the scopes requested.
    /// </summary>
    /// <value>
    /// The scopes requested.
    /// </value>
    public IEnumerable<string> ScopesRequested { get; set; }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    /// <value>
    /// The subject.
    /// </value>
    public string Subject { get; set; }

    /// <summary>
    /// Gets the identifier.
    /// </summary>
    /// <value>
    /// The identifier.
    /// </value>
    public string Id
    {
        get
        {
            var normalizedScopes = ScopesRequested?.OrderBy(x => x).Distinct().Aggregate((x, y) => x + ',' + y);
            var value = $"{ClientId}:{Subject}:{Nonce}:{normalizedScopes}";

            var bytes = Encoding.UTF8.GetBytes(value);
            var hash = SHA256.HashData(bytes);

            return Base64Url.Encode(hash);
        }
    }
}
