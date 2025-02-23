// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.IO;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;

namespace UnitTests.Common;

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

    public static SigningCredentials LoadSigningCredentials()
    {
        var cert = Load();
        return new SigningCredentials(new X509SecurityKey(cert), "RS256");
    }
}