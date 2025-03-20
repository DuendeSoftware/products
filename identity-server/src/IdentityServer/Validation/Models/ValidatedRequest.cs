// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Collections.Specialized;
using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Base class for a validate authorize or token request
/// </summary>
public class ValidatedRequest
{
    /// <summary>
    /// Gets or sets the raw request data
    /// </summary>
    /// <value>
    /// The raw.
    /// </value>
    public NameValueCollection Raw { get; set; } = null!;

    /// <summary>
    /// Gets or sets the client.
    /// </summary>
    /// <value>
    /// The client.
    /// </value>
    public Client Client { get; set; } = null!;

    /// <summary>
    /// The name of the issuer for the current request
    /// </summary>
    public string IssuerName { get; set; } = null!;

    /// <summary>
    /// Gets or sets the secret used to authenticate the client.
    /// </summary>
    /// <value>
    /// The parsed secret.
    /// </value>
    public ParsedSecret? Secret { get; set; }

    /// <summary>
    /// Gets or sets the effective access token lifetime for the current request.
    /// This value is initally read from the client configuration but can be modified in the request pipeline
    /// </summary>
    public int AccessTokenLifetime { get; set; }

    /// <summary>
    /// Gets or sets the client claims for the current request.
    /// This value is initally read from the client configuration but can be modified in the request pipeline
    /// </summary>
    public ICollection<Claim> ClientClaims { get; set; } = new HashSet<Claim>(new ClaimComparer());

    /// <summary>
    /// Gets or sets the effective access token type for the current request.
    /// This value is initally read from the client configuration but can be modified in the request pipeline
    /// </summary>
    public AccessTokenType AccessTokenType { get; set; }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    /// <value>
    /// The subject.
    /// </value>
    public ClaimsPrincipal? Subject { get; set; }

    /// <summary>
    /// Gets or sets the session identifier.
    /// </summary>
    /// <value>
    /// The session identifier.
    /// </value>
    public string? SessionId { get; set; }

    /// <summary>
    /// Gets or sets the identity server options.
    /// </summary>
    /// <value>
    /// The options.
    /// </value>
    public IdentityServerOptions Options { get; set; } = null!;

    /// <summary>
    /// Gets or sets the validated resources for the request.
    /// </summary>
    /// <value>
    /// The validated resources.
    /// </value>
    public ResourceValidationResult ValidatedResources { get; set; } = new ResourceValidationResult();

    /// <summary>
    /// Gets or sets the value of the confirmation method (will become the cnf claim). Must be a JSON object.
    /// </summary>
    /// <value>
    /// The confirmation.
    /// </value>
    public string? Confirmation { get; set; }

    /// <summary>
    /// The type of proof for the request
    /// </summary>
    public ProofType ProofType { get; set; }

    /// <summary>
    /// Gets or sets the client ID that should be used for the current request (this is useful for token exchange scenarios)
    /// </summary>
    /// <value>
    /// The client ID
    /// </value>
    public string ClientId { get; set; } = null!;

    /// <summary>
    /// Sets the client and the appropriate request specific settings.
    /// </summary>
    /// <param name="client">The client.</param>
    /// <param name="secret">The client secret (optional).</param>
    /// <param name="confirmation">The confirmation.</param>
    /// <exception cref="ArgumentNullException">client</exception>
    public void SetClient(Client client, ParsedSecret? secret = null, string confirmation = "")
    {
        Client = client ?? throw new ArgumentNullException(nameof(client));
        Secret = secret;
        Confirmation = confirmation;
        ClientId = client.ClientId;

        AccessTokenLifetime = client.AccessTokenLifetime;
        AccessTokenType = client.AccessTokenType;
        ClientClaims = client.Claims.Select(c => new Claim(c.Type, c.Value, c.ValueType)).ToList();
    }
}
