// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Duende.Bff.Configuration;

internal sealed record OidcConfiguration
{
    /// <summary>
    /// The client ID of the OpenID Connect client.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// The client secret of the OpenID Connect client.
    /// </summary>
    public string? ClientSecret { get; init; }

    /// <summary>
    /// The path to which the OpenID Connect client will redirect after authentication.
    /// </summary>
    public Uri? CallbackPath { get; init; }

    /// <summary>
    /// The authority component of the URI, which typically includes the host and port.
    /// </summary>
    public Uri? Authority { get; init; }

    /// <summary>
    /// The response type that the OpenID Connect client will request.
    /// </summary>
    public string? ResponseType { get; init; }

    /// <summary>
    /// The response mode that the OpenID Connect client will use to return the authentication response.
    /// </summary>
    public string? ResponseMode { get; init; }

    /// <summary>
    /// Whether to map inbound claims from the OpenID Connect provider to the user's claims in the application.
    /// </summary>
    public bool? MapInboundClaims { get; init; }

    /// <summary>
    /// Whether to save the tokens received from the OpenID Connect provider.
    /// </summary>
    public bool? SaveTokens { get; init; }

    /// <summary>
    /// The scopes that the OpenID Connect client will request from the OpenID Connect provider.
    /// </summary>
    public IEnumerable<string>? Scope { get; init; } = [];

    /// <summary>
    /// Whether to retrieve claims from the UserInfo endpoint of the OpenID Connect provider.
    /// </summary>
    public bool? GetClaimsFromUserInfoEndpoint { get; init; }

    internal void ApplyTo(OpenIdConnectOptions options)
    {
        if (Authority != null)
        {
            options.Authority = Authority.ToString();
        }

        if (ClientId != null)
        {
            options.ClientId = ClientId;
        }

        if (ClientSecret != null)
        {
            options.ClientSecret = ClientSecret;
        }

        if (ResponseType != null)
        {
            options.ResponseType = ResponseType;
        }
        if (CallbackPath != null)
        {
            options.CallbackPath = CallbackPath.ToString();
        }
        if (ResponseMode != null)
        {
            options.ResponseMode = ResponseMode;
        }
        if (MapInboundClaims != null)
        {
            options.MapInboundClaims = MapInboundClaims.Value;
        }
        if (SaveTokens != null)
        {
            options.SaveTokens = SaveTokens.Value;
        }
        if (GetClaimsFromUserInfoEndpoint != null)
        {
            options.GetClaimsFromUserInfoEndpoint = GetClaimsFromUserInfoEndpoint.Value;
        }
        if (Scope != null)
        {
            foreach (var scope in Scope)
            {
                options.Scope.Add(scope);
            }
        }

    }
}
