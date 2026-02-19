// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Internal.Saml.Infrastructure;

namespace UnitTests.Common;

/// <summary>
/// Mock implementation of <see cref="ISamlSigningService"/> for testing.
/// </summary>
internal class MockSamlSigningService : ISamlSigningService
{
    private readonly X509Certificate2 _certificate;

    public MockSamlSigningService(X509Certificate2 certificate) => _certificate = certificate;

    public Task<X509Certificate2> GetSigningCertificateAsync() => Task.FromResult(_certificate);

    public Task<string> GetSigningCertificateBase64Async()
    {
        var certBytes = _certificate.Export(X509ContentType.Cert);
        return Task.FromResult(Convert.ToBase64String(certBytes));
    }
}
