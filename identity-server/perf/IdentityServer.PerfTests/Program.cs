// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using BenchmarkDotNet.Running;
using IdentityServer.PerfTests.Services;

namespace IdentityServer.PerfTests
{
    class Program
    {
        static void Main(string[] args)
        {
            //var sub = new DefaultTokenServiceTest();
            //while (true)
            //{
            //    sub.TestTokenCreation().GetAwaiter().GetResult();
            //}

            BenchmarkRunner.Run<DefaultTokenServiceTest>();
        }
    }
}

