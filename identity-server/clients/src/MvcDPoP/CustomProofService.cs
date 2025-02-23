using Duende.AccessTokenManagement;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace MvcDPoP;

public class CustomProofService : DefaultDPoPProofService, IDPoPProofService
{
    public CustomProofService(IDPoPNonceStore dPoPNonceStore, ILogger<DefaultDPoPProofService> logger) : base(dPoPNonceStore, logger)
    {
    }

    public new Task<DPoPProof> CreateProofTokenAsync(DPoPProofRequest request)
    {
        if (request.Url.StartsWith("https://localhost:5005"))
        {
            // this shows how you can prevent DPoP from being used at an API endpoint
            // and this will cause the request to send the same token with the Bearer scheme
            //return Task.FromResult<DPoPProof>(null);
        }
        return base.CreateProofTokenAsync(request);
    }

    public new string GetProofKeyThumbprint(DPoPProofRequest request)
    {
        return base.GetProofKeyThumbprint(request);
    }
}
