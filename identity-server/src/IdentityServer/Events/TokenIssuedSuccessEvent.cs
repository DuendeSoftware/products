// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using System.Collections.Generic;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Events;

/// <summary>
/// Event for successful token issuance
/// </summary>
/// <seealso cref="Event" />
public class TokenIssuedSuccessEvent : Event
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TokenIssuedSuccessEvent"/> class.
    /// </summary>
    /// <param name="response">The response.</param>
    public TokenIssuedSuccessEvent(AuthorizeResponse response)
        : this()
    {
        ClientId = response.Request.ClientId;
        ClientName = response.Request.Client.ClientName;
        RedirectUri = response.RedirectUri;
        Endpoint = IdentityServerConstants.EndpointNames.Authorize;
        SubjectId = response.Request.Subject.GetSubjectId();
        Scopes = response.Scope;
        GrantType = response.Request.GrantType;

        var tokens = new List<Token>();
        if (response.IdentityToken != null)
        {
            tokens.Add(new Token(OidcConstants.TokenTypes.IdentityToken, response.IdentityToken));
        }
        if (response.Code != null)
        {
            tokens.Add(new Token(OidcConstants.ResponseTypes.Code, response.Code));
        }
        if (response.AccessToken != null)
        {
            tokens.Add(new Token(OidcConstants.TokenTypes.AccessToken, response.AccessToken));
        }
        Tokens = tokens;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenIssuedSuccessEvent"/> class.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <param name="request">The request.</param>
    public TokenIssuedSuccessEvent(TokenResponse response, TokenRequestValidationResult request)
        : this()
    {
        ClientId = request.ValidatedRequest.Client.ClientId;
        ClientName = request.ValidatedRequest.Client.ClientName;
        Endpoint = IdentityServerConstants.EndpointNames.Token;
        SubjectId = request.ValidatedRequest.Subject?.GetSubjectId();
        GrantType = request.ValidatedRequest.GrantType;

        if (GrantType == OidcConstants.GrantTypes.RefreshToken)
        {
            Scopes = request.ValidatedRequest.RefreshToken.AuthorizedScopes.ToSpaceSeparatedString();
        }
        else if (GrantType == OidcConstants.GrantTypes.AuthorizationCode)
        {
            Scopes = request.ValidatedRequest.AuthorizationCode.RequestedScopes.ToSpaceSeparatedString();
        }
        else
        {
            Scopes = request.ValidatedRequest.ValidatedResources?.RawScopeValues.ToSpaceSeparatedString();
        }

        var tokens = new List<Token>();
        if (response.IdentityToken != null)
        {
            tokens.Add(new Token(OidcConstants.TokenTypes.IdentityToken, response.IdentityToken));
        }
        if (response.RefreshToken != null)
        {
            tokens.Add(new Token(OidcConstants.TokenTypes.RefreshToken, response.RefreshToken));
        }
        if (response.AccessToken != null)
        {
            tokens.Add(new Token(OidcConstants.TokenTypes.AccessToken, response.AccessToken));
        }
        Tokens = tokens;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenIssuedSuccessEvent"/> class.
    /// </summary>
    protected TokenIssuedSuccessEvent()
        : base(EventCategories.Token,
            "Token Issued Success",
            EventTypes.Success,
            EventIds.TokenIssuedSuccess)
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
    /// Gets or sets the redirect URI.
    /// </summary>
    /// <value>
    /// The redirect URI.
    /// </value>
    public string RedirectUri { get; set; }

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

    /// <summary>
    /// Gets or sets the grant type.
    /// </summary>
    /// <value>
    /// The grant type.
    /// </value>
    public string GrantType { get; set; }

    /// <summary>
    /// Gets or sets the tokens.
    /// </summary>
    /// <value>
    /// The tokens.
    /// </value>
    public IEnumerable<Token> Tokens { get; set; }

    /// <summary>
    /// Data structure serializing issued tokens
    /// </summary>
    public class Token
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Token"/> class.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="value">The value.</param>
        public Token(string type, string value)
        {
            TokenType = type;
            TokenValue = Obfuscate(value);
        }

        /// <summary>
        /// Gets the type of the token.
        /// </summary>
        /// <value>
        /// The type of the token.
        /// </value>
        public string TokenType { get; }

        /// <summary>
        /// Gets the token value.
        /// </summary>
        /// <value>
        /// The token value.
        /// </value>
        public string TokenValue { get; }
    }
}