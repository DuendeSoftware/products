// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Cryptography.X509Certificates;

namespace Duende.IdentityServer.IntegrationTests.Common;

/// <summary>
/// Message handler that injects a client certificate into the HTTP request for testing mTLS scenarios
/// </summary>
public class MtlsMessageHandler : DelegatingHandler
{
    private readonly X509Certificate2 _clientCertificate;

    public MtlsMessageHandler(HttpMessageHandler innerHandler, X509Certificate2 clientCertificate)
        : base(innerHandler) => _clientCertificate = clientCertificate;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, Ct ct)
    {
        // Add the client certificate as a base64 encoded header for the test middleware to pick up
        if (_clientCertificate != null)
        {
            var certBytes = _clientCertificate.Export(X509ContentType.Cert);
            var certBase64 = Convert.ToBase64String(certBytes);
            request.Headers.Add("X-Test-Client-Certificate", certBase64);
        }

        return await base.SendAsync(request, ct);
    }
}
