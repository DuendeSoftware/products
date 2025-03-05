// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.Bff.Blazor.Client.UnitTests;

public class ServiceCollectionExtensionsTests
{
    [Theory]
    [InlineData("https://example.com/", "https://example.com/")]
    [InlineData("https://example.com", "https://example.com/")]
    public void When_base_address_option_is_set_AddBffBlazorClient_configures_HttpClient_base_address(string configuredRemoteAddress, string expectedBaseAddress)
    {
        var sut = new ServiceCollection();
        sut.AddBffBlazorClient();
        sut.Configure<BffClientBlazorOptions>(opt =>
        {
            opt.StateProviderBaseAddress = configuredRemoteAddress;
        });


        var sp = sut.BuildServiceProvider();
        var httpClientFactory = sp.GetService<IHttpClientFactory>();
        var httpClient = httpClientFactory?.CreateClient(BffClientAuthenticationStateProvider.HttpClientName);
        httpClient.ShouldNotBeNull();
        httpClient.BaseAddress.ShouldNotBeNull();
        httpClient.BaseAddress.AbsoluteUri.ShouldBe(expectedBaseAddress);
    }

    [Fact]
    public void When_base_address_option_is_default_AddBffBlazorClient_configures_HttpClient_base_address_from_host_env()
    {
        var expectedBaseAddress = "https://example.com/";

        var sut = new ServiceCollection();
        sut.AddBffBlazorClient();
        sut.AddSingleton<IWebAssemblyHostEnvironment>(new FakeWebAssemblyHostEnvironment()
        {
            BaseAddress = expectedBaseAddress
        });

        var sp = sut.BuildServiceProvider();
        var httpClientFactory = sp.GetService<IHttpClientFactory>();
        var httpClient = httpClientFactory?.CreateClient(BffClientAuthenticationStateProvider.HttpClientName);
        httpClient.ShouldNotBeNull();
        httpClient.BaseAddress.ShouldNotBeNull();
        httpClient.BaseAddress.AbsoluteUri.ShouldBe(expectedBaseAddress);
    }

    [Fact]

    public void AddLocalApiHttpClient_configures_HttpClient_base_address()
    {
        var sut = new ServiceCollection();

        sut.AddBffBlazorClient();
        sut.AddLocalApiHttpClient("clientName");
        sut.AddSingleton<IWebAssemblyHostEnvironment>(new FakeWebAssemblyHostEnvironment());
        sut.Configure<BffClientBlazorOptions>(opt =>
        {
            opt.RemoteApiBaseAddress = "Should_not_be_used";
            opt.RemoteApiPath = "should_not_be_used";
        });


        var sp = sut.BuildServiceProvider();
        var httpClientFactory = sp.GetService<IHttpClientFactory>();
        var httpClient = httpClientFactory?.CreateClient("clientName");
        httpClient.ShouldNotBeNull();
        httpClient.BaseAddress.ShouldNotBeNull();
        httpClient.BaseAddress.AbsoluteUri.ShouldBe(new FakeWebAssemblyHostEnvironment().BaseAddress);
    }

    private record FakeWebAssemblyHostEnvironment : IWebAssemblyHostEnvironment
    {
        public string Environment { get; set; } = "Development";
        public string BaseAddress { get; set; } = "https://example.com/";
    }

    [Theory]
    [InlineData("https://example.com/", "remote-apis", "https://example.com/remote-apis/")]
    [InlineData("https://example.com/", null, "https://example.com/remote-apis/")]
    [InlineData("https://example.com", null, "https://example.com/remote-apis/")]
    [InlineData("https://example.com", "custom/route/to/apis", "https://example.com/custom/route/to/apis/")]
    [InlineData("https://example.com/with/base/path", "custom/route/to/apis", "https://example.com/with/base/path/custom/route/to/apis/")]
    [InlineData("https://example.com/with/base/path/", "custom/route/to/apis", "https://example.com/with/base/path/custom/route/to/apis/")]
    [InlineData("https://example.com/with/base/path", "/custom/route/to/apis", "https://example.com/with/base/path/custom/route/to/apis/")]
    [InlineData("https://example.com/with/base/path/", "/custom/route/to/apis", "https://example.com/with/base/path/custom/route/to/apis/")]
    [InlineData("https://example.com/with/base/path", null, "https://example.com/with/base/path/remote-apis/")]
    public void AddRemoteApiHttpClient_configures_HttpClient_base_address(string? configuredRemoteAddress, string? configuredRemotePath, string expectedBaseAddress)
    {
        var sut = new ServiceCollection();
        sut.AddBffBlazorClient();
        sut.AddRemoteApiHttpClient("clientName");
        sut.Configure<BffClientBlazorOptions>(opt =>
        {
            if (configuredRemoteAddress != null)
            {
                opt.RemoteApiBaseAddress = configuredRemoteAddress;
            }
            if (configuredRemotePath != null)
            {
                opt.RemoteApiPath = configuredRemotePath;
            }
        });


        var sp = sut.BuildServiceProvider();
        var httpClientFactory = sp.GetService<IHttpClientFactory>();
        var httpClient = httpClientFactory?.CreateClient("clientName");
        httpClient.ShouldNotBeNull();
        httpClient.BaseAddress.ShouldNotBeNull();
        httpClient.BaseAddress.AbsoluteUri.ShouldBe(expectedBaseAddress);
    }

    [Fact]
    public void When_base_address_option_is_default_AddRemoteApiHttpClient_configures_HttpClient_base_address_from_host_env()
    {
        var hostBaseAddress = "https://example.com/";
        var expectedBaseAddress = "https://example.com/remote-apis/";

        var sut = new ServiceCollection();
        sut.AddBffBlazorClient();
        sut.AddRemoteApiHttpClient("clientName");
        sut.AddSingleton<IWebAssemblyHostEnvironment>(new FakeWebAssemblyHostEnvironment()
        {
            BaseAddress = hostBaseAddress
        });

        var sp = sut.BuildServiceProvider();
        var httpClientFactory = sp.GetService<IHttpClientFactory>();
        var httpClient = httpClientFactory?.CreateClient("clientName");
        httpClient.ShouldNotBeNull();
        httpClient.BaseAddress.ShouldNotBeNull();
        httpClient.BaseAddress.AbsoluteUri.ShouldBe(expectedBaseAddress);
    }

    [Fact]
    public void When_base_address_option_is_default_AddRemoteApiHttpClient_configures_HttpClient_base_address_from_host_env_and_config_callback_is_respected()
    {
        var hostBaseAddress = "https://example.com/";
        var expectedBaseAddress = "https://example.com/remote-apis/";

        var sut = new ServiceCollection();
        sut.AddBffBlazorClient();
        sut.AddRemoteApiHttpClient("clientName", c => c.Timeout = TimeSpan.FromSeconds(321));
        sut.AddSingleton<IWebAssemblyHostEnvironment>(new FakeWebAssemblyHostEnvironment()
        {
            BaseAddress = hostBaseAddress
        });

        var sp = sut.BuildServiceProvider();
        var httpClientFactory = sp.GetService<IHttpClientFactory>();
        var httpClient = httpClientFactory?.CreateClient("clientName");
        httpClient.ShouldNotBeNull();
        httpClient.BaseAddress.ShouldNotBeNull();
        httpClient.BaseAddress.AbsoluteUri.ShouldBe(expectedBaseAddress);
        httpClient.Timeout.ShouldBe(TimeSpan.FromSeconds(321));
    }

    [Fact]
    public void When_base_address_option_is_default_AddRemoteApiHttpClient_for_typed_clients_configures_HttpClient_base_address_from_host_env()
    {
        var hostBaseAddress = "https://example.com/";
        var expectedBaseAddress = "https://example.com/remote-apis/";

        var sut = new ServiceCollection();
        sut.AddBffBlazorClient();
        sut.AddTransient<ResolvesTypedClients>();
        sut.AddRemoteApiHttpClient<ResolvesTypedClients>();
        sut.AddSingleton<IWebAssemblyHostEnvironment>(new FakeWebAssemblyHostEnvironment()
        {
            BaseAddress = hostBaseAddress
        });

        var sp = sut.BuildServiceProvider();
        var wrapper = sp.GetService<ResolvesTypedClients>();
        var httpClient = wrapper?.Client;
        httpClient.ShouldNotBeNull();
        httpClient.BaseAddress.ShouldNotBeNull();
        httpClient.BaseAddress.AbsoluteUri.ShouldBe(expectedBaseAddress);
    }

    [Fact]
    public void When_base_address_option_is_default_AddRemoteApiHttpClient_for_typed_clients_configures_HttpClient_base_address_from_host_env_and_config_callback_is_respected()
    {
        var hostBaseAddress = "https://example.com/";
        var expectedBaseAddress = "https://example.com/remote-apis/";

        var sut = new ServiceCollection();
        sut.AddBffBlazorClient();
        sut.AddTransient<ResolvesTypedClients>();
        sut.AddRemoteApiHttpClient<ResolvesTypedClients>(c => c.Timeout = TimeSpan.FromSeconds(321));
        sut.AddSingleton<IWebAssemblyHostEnvironment>(new FakeWebAssemblyHostEnvironment()
        {
            BaseAddress = hostBaseAddress
        });

        var sp = sut.BuildServiceProvider();
        var wrapper = sp.GetService<ResolvesTypedClients>();
        var httpClient = wrapper?.Client;
        httpClient.ShouldNotBeNull();
        httpClient.BaseAddress.ShouldNotBeNull();
        httpClient.BaseAddress.AbsoluteUri.ShouldBe(expectedBaseAddress);
        httpClient.Timeout.ShouldBe(TimeSpan.FromSeconds(321));
    }

    private class ResolvesTypedClients(HttpClient client)
    {
        public HttpClient Client { get; } = client;
    }

    [Fact]
    public void AddBffBlazorClient_can_set_options_with_callback()
    {
        var expectedConfiguredValue = "some-path";
        var sut = new ServiceCollection();
        sut.AddBffBlazorClient(opt => opt.RemoteApiPath = expectedConfiguredValue);
        var sp = sut.BuildServiceProvider();
        var opts = sp.GetService<IOptions<BffClientBlazorOptions>>();
        opts.ShouldNotBeNull();
        opts.Value.RemoteApiPath.ShouldBe(expectedConfiguredValue);
    }
}
