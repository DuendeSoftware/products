using System.Net;
using Duende.Bff;
using Duende.Bff.Tests.TestHosts;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Xunit.Abstractions;

namespace Bff.Blazor.UnitTests
{
    public class BffBlazorTests : OutputWritingTestBase
    {
        protected readonly IdentityServerHost IdentityServerHost;
        protected ApiHost ApiHost;
        protected BffBlazorHost BffHost;

        public BffBlazorTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            IdentityServerHost = new IdentityServerHost(WriteLine);
            ApiHost = new ApiHost(WriteLine, IdentityServerHost, "scope1");

            BffHost = new BffBlazorHost(WriteLine, IdentityServerHost, ApiHost, "spa");

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

        [Fact]
        public async Task Can_get_home()
        {
            var response = await BffHost.HttpClient.GetAsync("/");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }


        public override async Task InitializeAsync()
        {
            await IdentityServerHost.InitializeAsync();
            await ApiHost.InitializeAsync();
            await BffHost.InitializeAsync();
            await base.InitializeAsync();
        }

        public override async Task DisposeAsync()
        {
            await ApiHost.DisposeAsync();
            await BffHost.DisposeAsync();
            await IdentityServerHost.DisposeAsync();
            await base.DisposeAsync();

        }
    }
}
