using Hosts.ServiceDefaults;
using Hosts.Tests.TestInfra;
using Shouldly;
using Xunit.Abstractions;

namespace Hosts.Tests;

public class BffTests : IntegrationTestBase
{
    private readonly HttpClient _httpClient;
    private readonly BffClient _bffClient;

    public BffTests(ITestOutputHelper output, AppHostFixture fixture) : base(output: output, fixture: fixture)
    {
        _httpClient = CreateHttpClient(AppHostServices.Bff);
        _bffClient = new BffClient(CreateHttpClient(AppHostServices.Bff));
    }

    [SkippableFact]
    public async Task Can_invoke_home()
    {
        var response = await _httpClient.GetAsync("/");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [SkippableFact]
    public async Task Can_initiate_login()
    {

        var response = await _httpClient.GetAsync("/");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await _bffClient.TriggerLogin();

        // Verify that there are user claims
        var claims = await _bffClient.GetUserClaims();
        claims.Any().ShouldBeTrue();
    }

    [SkippableTheory]
    [InlineData("/local/self-contained")]
    [InlineData("/local/invokes-external-api")]
    [InlineData("/api/user-token")]
    [InlineData("/api/client-token")]
    [InlineData("/api/user-or-client-token")]
    [InlineData("/api/anonymous")]
    [InlineData("/api/optional-user-token")]
    [InlineData("/api/impersonation")]
    [InlineData("/api/audience-constrained")]
    public async Task Once_authenticated_can_call_proxied_urls(string url)
    {
        await _bffClient.TriggerLogin();
        await _bffClient.InvokeApi(url);
    }

    [SkippableFact]
    public async Task Can_logout()
    {
        await _bffClient.TriggerLogin();
        await _bffClient.TriggerLogout();

        await _bffClient.InvokeApi(url: "/local/self-contained", expectedResponse: HttpStatusCode.Unauthorized);
    }
}