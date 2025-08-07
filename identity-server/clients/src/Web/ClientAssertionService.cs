// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;

namespace Web;

public class ClientAssertionService(AssertionService assertionService) : IClientAssertionService
{
    public Task<ClientAssertion?> GetClientAssertionAsync(string? clientName = null, TokenRequestParameters? parameters = null)
    {
        var assertion = new ClientAssertion
        {
            Type = OidcConstants.ClientAssertionTypes.JwtBearer,
            Value = assertionService.CreateClientToken()
        };

        return Task.FromResult(assertion)!;
    }
}
