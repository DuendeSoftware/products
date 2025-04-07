// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace MvcJarJwt;

public class OidcEvents : OpenIdConnectEvents
{
    private readonly AssertionService _assertionService;

    public OidcEvents(AssertionService assertionService) => _assertionService = assertionService;

    public override Task AuthorizationCodeReceived(AuthorizationCodeReceivedContext context)
    {
        context.TokenEndpointRequest.ClientAssertionType = OidcConstants.ClientAssertionTypes.JwtBearer;
        context.TokenEndpointRequest.ClientAssertion = _assertionService.CreateClientToken();

        return Task.CompletedTask;
    }

    public override Task PushAuthorization(PushedAuthorizationContext context)
    {
        var request = _assertionService.SignAuthorizationRequest(context.ProtocolMessage);
        var clientId = context.ProtocolMessage.ClientId;

        context.ProtocolMessage.Parameters.Clear();
        context.ProtocolMessage.ClientId = clientId;
        context.ProtocolMessage.ClientAssertionType = OidcConstants.ClientAssertionTypes.JwtBearer;
        context.ProtocolMessage.ClientAssertion = _assertionService.CreateClientToken();
        context.ProtocolMessage.SetParameter("request", request);

        return Task.CompletedTask;
    }
}
