// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Json.Serialization;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Configuration.Models.DynamicClientRegistration;

/// <summary>
/// Represents the response to a successful dynamic client registration request.
/// </summary>
public class DynamicClientRegistrationResponse : DynamicClientRegistrationRequest, IDynamicClientRegistrationResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicClientRegistrationResponse"/> class.
    /// </summary>
    public DynamicClientRegistrationResponse()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see
    /// cref="DynamicClientRegistrationResponse"/> class copying properties from
    /// the specified request and client. This tries to copy from the client's
    /// properties, and only uses the request if it must. Doing so means that the
    /// response will better reflect the actually created client record.
    /// </summary>
    /// <param name="request">The request used to initialize the
    /// response.</param>
    /// <param name="client">The client used to initialize the response.</param>
    public DynamicClientRegistrationResponse(DynamicClientRegistrationRequest request, Client client)
    {
        //// Software Statement
        SoftwareStatement = request.SoftwareStatement;
        SoftwareId = request.SoftwareId;
        SoftwareVersion = request.SoftwareVersion;

        //// Grant Types
        GrantTypes = client.AllowedGrantTypes;
        if (client.AllowOfflineAccess)
        {
            GrantTypes.Add(OidcConstants.GrantTypes.RefreshToken);
            // Sliding refresh tokens can use both absolute and sliding lifetime,
            // but absolute refresh tokens never use the sliding lifetime
            AbsoluteRefreshTokenLifetime = client.AbsoluteRefreshTokenLifetime;
            if (client.RefreshTokenExpiration == TokenExpiration.Sliding)
            {
                SlidingRefreshTokenLifetime = client.SlidingRefreshTokenLifetime;
            }
            UpdateAccessTokenClaimsOnRefresh = client.UpdateAccessTokenClaimsOnRefresh;
            RefreshTokenExpiration = client.RefreshTokenExpiration.ToString();
            RefreshTokenUsage = client.RefreshTokenUsage.ToString();
        }
        if (GrantTypes.Contains(GrantType.AuthorizationCode))
        {
            AuthorizationCodeLifetime = client.AuthorizationCodeLifetime;
        }

        //// Redirect Uris
        if (client.RedirectUris.Count > 0)
        {
            RedirectUris = [.. client.RedirectUris.Select(s => new Uri(s))];
        }

        //// Scopes
        Scope = string.Join(' ', client.AllowedScopes);

        //// Secrets
        JwksUri = request.JwksUri;
        Jwks = request.Jwks;
        TokenEndpointAuthenticationMethod = request.TokenEndpointAuthenticationMethod;
        RequireSignedRequestObject = InteractiveFlowsEnabled(client) ?
            client.RequireRequestObject : null;

        //// Client Name
        ClientName = client.ClientName;

        if (GrantTypes.Contains(GrantType.AuthorizationCode))
        {
            //// Logout Parameters
            PostLogoutRedirectUris = [.. client.PostLogoutRedirectUris.Select(s => new Uri(s))];

            FrontChannelLogoutUri = ToUri(client.FrontChannelLogoutUri);
            // If there is no FrontChannelLogoutUri, then we hide the session required flag because it would be confusing
            // (the user probably didn't set it, and they can't use it without the uri anyway)
            FrontChannelLogoutSessionRequired = FrontChannelLogoutUri != null ? client.FrontChannelLogoutSessionRequired : null;
            BackChannelLogoutUri = ToUri(client.BackChannelLogoutUri);
            // Again, we hide this flag when there's no corresponding uri, just as for the FrontChannel case.
            BackChannelLogoutSessionRequired = BackChannelLogoutUri != null ? client.BackChannelLogoutSessionRequired : null;

            //// Max Age
            DefaultMaxAge = client.UserSsoLifetime;

            //// User Interface
            LogoUri = ToUri(client.LogoUri);
            InitiateLoginUri = ToUri(client.InitiateLoginUri);
            EnableLocalLogin = client.EnableLocalLogin;
            IdentityProviderRestrictions = [.. client.IdentityProviderRestrictions];
            RequireConsent = client.RequireConsent;
            ClientUri = ToUri(client.ClientUri);
            if (RequireConsent == true)
            {
                AllowRememberConsent = client.AllowRememberConsent;
                ConsentLifetime = client.ConsentLifetime;
            }
        }

        //// Public Clients
        AllowedCorsOrigins = InteractiveFlowsEnabled(client) ?
            [.. client.AllowedCorsOrigins] :
            null;

        RequireClientSecret = client.RequireClientSecret;

        //// Access Token
        AccessTokenType = client.AccessTokenType.ToString();
        AccessTokenLifetime = client.AccessTokenLifetime;

        //// ID Token
        if (client.AllowedScopes.Contains("openid"))
        {
            IdentityTokenLifetime = client.IdentityTokenLifetime;
            AllowedIdentityTokenSigningAlgorithms = client.AllowedIdentityTokenSigningAlgorithms.Count > 0 ?
                client.AllowedIdentityTokenSigningAlgorithms : null;
        }

        //// Server Side Sessions
        CoordinateLifetimeWithUserSession = client.CoordinateLifetimeWithUserSession;

        //// Response Types
        ResponseTypes = InteractiveFlowsEnabled(client) ? new List<string> { "code" } : null;

        //// Extensions
        Extensions = request.Extensions;

        //// Remove possible duplicate values from the extensions
        // Some properties are not included in the request object, but are
        // included in the response. Customizations might want to allow the
        // request to specify those properties, and they can do so in the
        // extensions dictionary. We want to echo back to the user any extension
        // values that they set, but also avoid duplicating values between the
        // elements of the Extensions dictionary and properties on the response
        // model itself.
        //
        // If a value is added to the extensions that also is a property of the
        // response object, then both values will be serialized. This at best is
        // redundant, and at worst results in conflicting values.
        //
        // For example, if a customization used
        // Request.Extensions["client_secret"] to set the Response.ClientSecret,
        // we don't want to copy Request.Extensions["client_secret"] to
        // Response.Extensions["client_secret"], because that will result in two
        // redundant "client_secret" properties in the serialized response. And
        // if a customization hasn't used Request.Extensions["client_secret"] to
        // set Response.ClientSecret, we still don't want to copy
        // Request.Extensions["client_secret"] to
        // Response.Extensions["client_secret"], because that would result in
        // two "client_secret" properties with inconsistent values in the
        // serialized response.
        //
        // Thus, after we copy the Extensions from the request to the response,
        // we remove any values from the Extensions that also have specific
        // properties on the response object. We don't need to try to remove
        // values from the Extensions that have specific properties in the
        // request object, because those values will get bound to the
        // properties, not the Extensions.
        Extensions.Remove(OidcConstants.RegistrationResponse.ClientSecret);
        Extensions.Remove(OidcConstants.RegistrationResponse.ClientSecretExpiresAt);
        Extensions.Remove(OidcConstants.ClientMetadata.ResponseTypes);
    }

    private static Uri? ToUri(string? s) =>
        s != null ? new Uri(s) : null;

    private static bool InteractiveFlowsEnabled(Client c) =>
        c.AllowedGrantTypes.Contains(GrantType.AuthorizationCode);

    /// <summary>
    /// Gets or sets the client ID.
    /// </summary>
    [JsonPropertyName(OidcConstants.RegistrationResponse.ClientId)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client secret.
    /// </summary>
    [JsonPropertyName(OidcConstants.RegistrationResponse.ClientSecret)]
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the expiration time of the client secret.
    /// </summary>
    [JsonPropertyName(OidcConstants.RegistrationResponse.ClientSecretExpiresAt)]
    public long? ClientSecretExpiresAt { get; set; }

    /// <summary>
    /// List of the OAuth 2.0 response type strings that the client can use at
    /// the authorization endpoint.
    /// </summary>
    /// <remarks>
    /// This will either be ["code"], when using the authorization code grant,
    /// or omitted from the response when not using authorization code grant.
    /// Other grants, such as the implicit or hybrid grants, that would allow
    /// the use of other values are not supported.
    /// </remarks>
    [JsonPropertyName(OidcConstants.ClientMetadata.ResponseTypes)]
    public ICollection<string>? ResponseTypes { get; set; } = new HashSet<string>();
}
