// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Internal.Saml.SingleLogout.Models;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Services;

namespace Duende.IdentityServer.Internal.Saml.SingleLogout;

internal class LogoutResponseBuilder(
    IIssuerNameService issuerNameService,
    TimeProvider timeProvider)
{
    internal async Task<LogoutResponse> BuildSuccessResponseAsync(
        RequestId logoutRequestId,
        SamlServiceProvider serviceProvider,
        string? relayState)
    {
        var issuer = await issuerNameService.GetCurrentAsync();
        var destination = serviceProvider.SingleLogoutServiceUrl ?? throw new InvalidOperationException("No SingleLogout service url configured");

        return new LogoutResponse
        {
            Id = ResponseId.New(),
            Version = SamlVersion.V2,
            IssueInstant = timeProvider.GetUtcNow().UtcDateTime,
            Destination = destination.Location,
            Issuer = issuer,
            InResponseTo = logoutRequestId.ToString(),
            Status = new Status
            {
                StatusCode = SamlStatusCode.Success
            },
            ServiceProvider = serviceProvider,
            RelayState = relayState
        };
    }

    internal async Task<LogoutResponse> BuildErrorResponseAsync(
        SamlLogoutRequest request,
        SamlServiceProvider serviceProvider,
        SamlError error)
    {
        var issuer = await issuerNameService.GetCurrentAsync();
        var destination = serviceProvider.SingleLogoutServiceUrl ?? throw new InvalidOperationException("No SingleLogout service url configured");

        return new LogoutResponse
        {
            Id = ResponseId.New(),
            Version = SamlVersion.V2,
            IssueInstant = timeProvider.GetUtcNow().UtcDateTime,
            Destination = destination.Location,
            Issuer = issuer,
            InResponseTo = request.LogoutRequest.Id.ToString(),
            Status = new Status
            {
                StatusCode = error.StatusCode,
                StatusMessage = error.Message,
                NestedStatusCode = error.SubStatusCode
            },
            ServiceProvider = serviceProvider,
            RelayState = request.RelayState
        };
    }
}
