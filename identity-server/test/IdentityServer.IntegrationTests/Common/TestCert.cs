// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Cryptography.X509Certificates;

namespace Duende.IdentityServer.IntegrationTests.Common;

internal static class TestCert
{
    public static X509Certificate2 Load()
    {
        var cert = Path.Combine(System.AppContext.BaseDirectory, "identityserver_testing.pfx");
#pragma warning disable SYSLIB0057 // Type or member is obsolete
        // TODO - Use X509CertificateLoader in a future release (when we drop NET8 support)
        return new X509Certificate2(cert, "password");
#pragma warning restore SYSLIB0057 // Type or member is obsolete
    }
}
