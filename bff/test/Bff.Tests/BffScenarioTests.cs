// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement;
using Duende.Bff.Tests.TestInfra;
namespace Duende.Bff.Tests;

public class BffScenarioTests : BffTestBase
{
    [Fact]
    public async Task When_using_bff_as_host_and_client_credentials_token_manager_with_no_http_context_still_works()
    {
        var workerClientId = "worker.client.id";
        _ = IdentityServer.AddClient(workerClientId, Bff.Url());
        var contentReceived = new TaskCompletionSource<string>();
        var workerStarted = new TaskCompletionSource();

        Bff.OnConfigureServices += services =>
        {
            _ = services.AddClientCredentialsTokenManagement()
                .AddClient(ClientCredentialsClientName.Parse("worker.client"), client =>
                {
                    client.TokenEndpoint = new Uri(IdentityServer.Url(), "/connect/token");
                    client.ClientId = ClientId.Parse(workerClientId);
                    client.ClientSecret = ClientSecret.Parse(The.ClientSecret);
                    client.Scope = Scope.Parse(The.Scope);
                    client.HttpClient = new HttpClient(Internet, disposeHandler: false);
                });

            _ = services.AddClientCredentialsHttpClient("worker",
                    ClientCredentialsClientName.Parse("worker.client"),
                    client => { client.BaseAddress = Api.Url(); })
                .ConfigurePrimaryHttpMessageHandler(() => Internet);

            _ = services.AddSingleton(contentReceived);
            _ = services.AddSingleton(workerStarted);
            _ = services.AddHostedService<BackgroundWorker>();
        };
        await InitializeAsync();
        workerStarted.SetResult();
        var content = await contentReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        content.ShouldNotBeNullOrEmpty();
    }

    internal class BackgroundWorker(
        IHttpClientFactory httpClientFactory,
        TaskCompletionSource<string> contentReceived,
        TaskCompletionSource workerIsAllowedToStart) : BackgroundService
    {
        protected override async Task ExecuteAsync(Ct stoppingToken)
        {
            await workerIsAllowedToStart.Task;

            var client = httpClientFactory.CreateClient("worker");

            try
            {
                var response = await client.GetAsync("/", stoppingToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(stoppingToken);
                    _ = contentReceived.TrySetResult(content);
                }
                else
                {
                    _ = contentReceived.TrySetException(
                        new Exception($"Request failed with status code: {response.StatusCode}"));
                }
            }
            catch (Exception ex)
            {
                _ = contentReceived.TrySetException(ex);
            }
        }
    }
}
