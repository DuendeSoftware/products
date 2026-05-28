// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Saml.Services;

namespace UnitTests.Common;

/// <summary>
/// Mock implementation of <see cref="ISamlSigningService"/> for testing.
/// </summary>
internal class MockSamlSigningService : ISamlSigningService
{
    private readonly X509Certificate2 _certificate;

    public MockSamlSigningService(X509Certificate2 certificate) => _certificate = certificate;

    public Task<X509Certificate2> GetSigningCertificateAsync(Ct _) => Task.FromResult(_certificate);

    public Task<string> GetSigningCertificateBase64Async(Ct _)
    {
        var certBytes = _certificate.Export(X509ContentType.Cert);
        return Task.FromResult(Convert.ToBase64String(certBytes));
    }

    public Task<IReadOnlyList<X509Certificate2>> GetAllSigningCertificatesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<X509Certificate2>>([_certificate]);
}
