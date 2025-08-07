// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace MvcJarUriJwt;

public class OidcEvents : OpenIdConnectEvents
{
    private readonly AssertionService _assertionService;
    private readonly RequestUriService _requestUriService;

    public OidcEvents(AssertionService assertionService, RequestUriService requestUriService)
    {
        _assertionService = assertionService;
        _requestUriService = requestUriService;
    }

    public override Task AuthorizationCodeReceived(AuthorizationCodeReceivedContext context)
    {
        context.TokenEndpointRequest.ClientAssertionType = OidcConstants.ClientAssertionTypes.JwtBearer;
        context.TokenEndpointRequest.ClientAssertion = _assertionService.CreateClientToken();

        return Task.CompletedTask;
    }

    public override Task RedirectToIdentityProvider(RedirectContext context)
    {
        var request = _assertionService.SignAuthorizationRequest(context.ProtocolMessage);
        var id = _requestUriService.Set(request);

        var clientId = context.ProtocolMessage.ClientId;
        var redirectUri = context.ProtocolMessage.RedirectUri;

        context.ProtocolMessage.Parameters.Clear();
        context.ProtocolMessage.ClientId = clientId;
        context.ProtocolMessage.RedirectUri = redirectUri;
        context.ProtocolMessage.SetParameter("request_uri", $"https://localhost:44305/ro?id={id}");

        return Task.CompletedTask;
    }
}
