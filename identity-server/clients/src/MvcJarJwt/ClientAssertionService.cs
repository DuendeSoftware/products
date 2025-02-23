using System.Threading.Tasks;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.AccessTokenManagement;

namespace MvcJarJwt;

public class ClientAssertionService : IClientAssertionService
{
    private readonly AssertionService _assertionService;

    public ClientAssertionService(AssertionService assertionService) 
    {
        _assertionService = assertionService;
    }

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