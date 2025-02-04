// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.TestHosts
{
    public class BffIntegrationTestBase : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;
        protected readonly IdentityServerHost IdentityServerHost;
        protected ApiHost ApiHost;
        protected BffHost BffHost;
        protected BffHostUsingResourceNamedTokens BffHostWithNamedTokens;

        public BffIntegrationTestBase(ITestOutputHelper output)
        {
            _output = output;
            IdentityServerHost = new IdentityServerHost(output.WriteLine);
            ApiHost = new ApiHost(_output.WriteLine, IdentityServerHost, "scope1");
            BffHost = new BffHost(_output.WriteLine, IdentityServerHost, ApiHost, "spa");
            BffHostWithNamedTokens = new BffHostUsingResourceNamedTokens(_output.WriteLine, IdentityServerHost, ApiHost, "spa");

            IdentityServerHost.Clients.Add(new Client
            {
                ClientId = "spa",
                ClientSecrets = { new Secret("secret".Sha256()) },
                AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
                RedirectUris = { "https://app/signin-oidc" },
                PostLogoutRedirectUris = { "https://app/signout-callback-oidc" },
                BackChannelLogoutUri = "https://app/bff/backchannel",
                AllowOfflineAccess = true,
                AllowedScopes = { "openid", "profile", "scope1" }
            });
            
            
            IdentityServerHost.OnConfigureServices += services => {
                services.AddTransient<IBackChannelLogoutHttpClient>(provider => 
                    new DefaultBackChannelLogoutHttpClient(
                        BffHost!.HttpClient, 
                        provider.GetRequiredService<ILoggerFactory>(), 
                        provider.GetRequiredService<ICancellationTokenProvider>()));

                services.AddSingleton<DefaultAccessTokenRetriever>();
            };
            
        }

        public async Task Login(string sub)
        {
            await IdentityServerHost.IssueSessionCookieAsync(new Claim("sub", sub));
        }

        public async Task InitializeAsync()
        {
            await IdentityServerHost.InitializeAsync();
            await ApiHost.InitializeAsync();
            await BffHost.InitializeAsync();
            await BffHostWithNamedTokens.InitializeAsync();

        }

        public async Task DisposeAsync()
        {
            await ApiHost.DisposeAsync();
            await BffHost.DisposeAsync();
            await BffHostWithNamedTokens.DisposeAsync();
            await IdentityServerHost.DisposeAsync();
        }
    }
}
