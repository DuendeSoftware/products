// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using BenchmarkDotNet.Attributes;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Validation;
using IdentityServer.PerfTest.Infrastructure;
using System.Security.Claims;
using System.Threading.Tasks;

namespace IdentityServer.PerfTests.Services
{
    public class DefaultTokenServiceTest : TestBase<InMemoryIdentityServerContainer>
    {
        private readonly ITokenService _subject;
        private readonly ClaimsPrincipal _principal;
        private readonly Client _client;
        private readonly ApiScope _scope;

        public DefaultTokenServiceTest()
        {
            Container.OnConfigureIdentityServerOptions += opts => {
                opts.IssuerUri = "https://server";
            };

            Container.Clients.Add(_client = new Client
            {
                ClientId = "client",
                ClientSecrets = { new Secret("secret".Sha256()) },
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                AllowedScopes = { "scope1" },
            });

            Container.ApiScopes.Add(_scope = new ApiScope("scope1"));
            Container.Reset();

            _subject = Container.ResolveService<ITokenService>();

            _principal = new IdentityServerUser("sub") 
            { 
                AuthenticationTime = System.DateTime.Now, 
                IdentityProvider = "local" 
            }.CreatePrincipal();
        }


        [Benchmark(Baseline = true)]
        public async Task TestTokenCreation()
        {
            var token = await _subject.CreateIdentityTokenAsync(new TokenCreationRequest
            {
                Nonce = "nonce",
                Subject = _principal,
                ValidatedRequest = new ValidatedRequest
                {
                    Client = _client,
                },
                ValidatedResources = new ResourceValidationResult(new Resources() { ApiScopes = new[] { _scope } }),
            });
            var jwt = await _subject.CreateSecurityTokenAsync(token);
            //System.Console.WriteLine(jwt);
        }
    }
}

