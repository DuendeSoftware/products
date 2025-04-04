// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;

namespace MvcJarUriJwt;

public class ClientAssertionService : IClientAssertionService
{
    private readonly AssertionService _assertionService;

    public ClientAssertionService(AssertionService assertionService) => _assertionService = assertionService;

    public Task<ClientAssertion> GetClientAssertionAsync(string clientName = null, TokenRequestParameters parameters = null)
    {
        var assertion = new ClientAssertion
        {
            Type = OidcConstants.ClientAssertionTypes.JwtBearer,
            Value = _assertionService.CreateClientToken()
        };

        return Task.FromResult(assertion);
    }
}
