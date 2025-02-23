// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using BenchmarkDotNet.Attributes;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace IdentityServer.PerfTest.Infrastructure
{
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
        public void PostTest()
        {
            Container.Dispose();
        }
    }
}

