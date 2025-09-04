// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Duende.IdentityServer.IntegrationTests.Common;

/// <summary>
/// Middleware for testing mTLS scenarios by injecting client certificates from request properties
/// </summary>
public class MtlsTestMiddleware
{
    private readonly RequestDelegate _next;

    public MtlsTestMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Check if the request has a client certificate in its properties
        if (context.Request.HttpContext.Features.Get<IHttpRequestFeature>() is IHttpRequestFeature requestFeature)
        {
            // Try to get the certificate from request properties (injected by MtlsMessageHandler)
            if (context.Features.Get<X509Certificate2>() == null)
            {
                // Look for certificate in request headers or properties
                // This is a simplified approach for testing - in real scenarios the certificate
                // would be part of the TLS handshake
                var cert = GetClientCertificateFromRequest(context);
                if (cert != null)
                {
                    // Create a custom feature to hold the client certificate
                    var tlsFeature = new TlsConnectionFeature { ClientCertificate = cert };
                    context.Features.Set<ITlsConnectionFeature>(tlsFeature);
                }
            }
        }

        await _next(context);
    }

    private static X509Certificate2 GetClientCertificateFromRequest(HttpContext context)
    {
        // For testing purposes, we'll try to get the certificate from a custom header
        // This simulates what would normally be provided by the TLS layer
        if (context.Request.Headers.TryGetValue("X-Test-Client-Certificate", out var certHeader))
        {
            try
            {
                var certBytes = Convert.FromBase64String(certHeader.First());
#pragma warning disable SYSLIB0057 // Type or member is obsolete
                return new X509Certificate2(certBytes);
#pragma warning restore SYSLIB0057 // Type or member is obsolete
            }
            catch
            {
                // Ignore invalid certificate data
            }
        }

        return null;
    }
}

/// <summary>
/// Custom implementation of ITlsConnectionFeature for testing
/// </summary>
public class TlsConnectionFeature : ITlsConnectionFeature
{
    public X509Certificate2 ClientCertificate { get; set; }

    public Task<X509Certificate2> GetClientCertificateAsync(CancellationToken cancellationToken)
        => Task.FromResult(ClientCertificate);
}
