// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.AccessTokenManagement;
using Duende.Bff.Tests.TestInfra;
using Xunit.Abstractions;

namespace Duende.Bff.Tests;

public class BffScenarioTests(ITestOutputHelper output) : BffTestBase(output)
{
    [Fact]
    public async Task When_using_bff_as_host_and_client_credentials_token_manager_with_no_http_context_still_works()
    {
        var workerClientId = "worker.client.id";
        IdentityServer.AddClient(workerClientId, Bff.Url());
        var contentReceived = new TaskCompletionSource<string>();
        var workerStarted = new TaskCompletionSource();

        Bff.OnConfigureServices += services =>
        {
            services.AddClientCredentialsTokenManagement()
                .AddClient(ClientCredentialsClientName.Parse("worker.client"), client =>
                {
                    client.TokenEndpoint = new Uri(IdentityServer.Url(), "/connect/token");
                    client.ClientId = ClientId.Parse(workerClientId);
                    client.ClientSecret = ClientSecret.Parse(The.ClientSecret);
                    client.Scope = Scope.Parse(The.Scope);
                    client.HttpClient = new HttpClient(Internet, disposeHandler: false);
                });

            services.AddClientCredentialsHttpClient("worker",
                    ClientCredentialsClientName.Parse("worker.client"),
                    client => { client.BaseAddress = Api.Url(); })
                .ConfigurePrimaryHttpMessageHandler(() => Internet);

            services.AddSingleton(contentReceived);
            services.AddSingleton(workerStarted);
            services.AddHostedService<BackgroundWorker>();
        };
        await InitializeAsync();
        workerStarted.SetResult();
        var content = await contentReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        content.ShouldNotBeNullOrEmpty();
    }

    internal class BackgroundWorker(
        IHttpClientFactory httpClientFactory,
        TaskCompletionSource<string> contentReceived,
        TaskCompletionSource workerIsAllowedToStart) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await workerIsAllowedToStart.Task;

            var client = httpClientFactory.CreateClient("worker");

            try
            {
                var response = await client.GetAsync("/", stoppingToken);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(stoppingToken);
                    contentReceived.TrySetResult(content);
                }
                else
                {
                    contentReceived.TrySetException(
                        new Exception($"Request failed with status code: {response.StatusCode}"));
                }
            }
            catch (Exception ex)
            {
                contentReceived.TrySetException(ex);
            }
        }
    }
}
