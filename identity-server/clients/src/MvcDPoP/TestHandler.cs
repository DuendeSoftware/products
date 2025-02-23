using Microsoft.Extensions.Logging;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MvcDPoP;

public class TestHandler : DelegatingHandler
{
    private readonly ILogger<TestHandler> _logger;

    public TestHandler(ILogger<TestHandler> logger)
    {
        _logger = logger;
    }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        if (response.Headers.Contains("WWW-Authenticate"))
        {
            foreach(var value in response.Headers.WwwAuthenticate)
            {
                _logger.LogInformation("Response from API {url}, WWW-Authenticate: {header}", request.RequestUri.AbsoluteUri, value.ToString());
            }
        }
        if (response.Headers.TryGetValues("DPoP-Nonce", out var header))
        {
            var nonce = header.First().ToString();
            _logger.LogInformation("Response from API {url}, nonce: {nonce}", request.RequestUri.AbsoluteUri, nonce);
        }
        return response;
    }
}
