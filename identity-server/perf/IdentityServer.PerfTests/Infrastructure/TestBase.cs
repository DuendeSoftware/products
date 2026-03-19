// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography.X509Certificates;
using BenchmarkDotNet.Attributes;

// TODO: remove pragma?
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace IdentityServer.PerfTest.Infrastructure;
#pragma warning restore IDE0130

// TODO: Update to newer X509Certificate2 implementation
#pragma warning disable SYSLIB0057 // X509Certificate2 constructor used here is deprecated from net10.0 onwards.

public class TestBase<T>
    where T : IdentityServerContainer, new()
{
    public static X509Certificate2 Cert { get; }

    static TestBase()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "client.pfx");
        Cert = new X509Certificate2(path, "password");
    }

    protected T Container = new T();

    [GlobalCleanup]
    public void PostTest() => Container.Dispose();
}
