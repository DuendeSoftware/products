// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace Web;

public class ConfigureAssertionsAndJar(AssertionService assertionService) : IPostConfigureOptions<OpenIdConnectOptions>
{

    private Func<AuthorizationCodeReceivedContext, Task> CreateCallback(Func<AuthorizationCodeReceivedContext, Task> inner)
    {
        async Task Callback(AuthorizationCodeReceivedContext context)
        {
            context.TokenEndpointRequest!.ClientAssertionType = OidcConstants.ClientAssertionTypes.JwtBearer;
            context.TokenEndpointRequest.ClientAssertion = assertionService.CreateClientToken();
            await inner.Invoke(context);
        }

        return Callback;
    }

    private Func<PushedAuthorizationContext, Task> CreateCallback(Func<PushedAuthorizationContext, Task> inner)
    {
        async Task Callback(PushedAuthorizationContext context)
        {
            var request = assertionService.SignAuthorizationRequest(context.ProtocolMessage);
            var clientId = context.ProtocolMessage.ClientId;

            context.ProtocolMessage.Parameters.Clear();
            context.ProtocolMessage.ClientId = clientId;
            context.ProtocolMessage.ClientAssertionType = OidcConstants.ClientAssertionTypes.JwtBearer;
            context.ProtocolMessage.ClientAssertion = assertionService.CreateClientToken();
            context.ProtocolMessage.SetParameter("request", request);
            await inner.Invoke(context);
        }
        return Callback;
    }
    public void PostConfigure(string? name, OpenIdConnectOptions options)
    {
        // Configure client assertions for authentication
        options.Events.OnAuthorizationCodeReceived = CreateCallback(options.Events.OnAuthorizationCodeReceived);

        // Use client assertions and sign token requests
        options.Events.OnPushAuthorization = CreateCallback(options.Events.OnPushAuthorization);
    }
}
