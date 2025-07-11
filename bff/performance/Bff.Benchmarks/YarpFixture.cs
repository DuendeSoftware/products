// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Bff.Benchmarks.Hosts;

namespace Bff.Benchmarks;

public class YarpFixture : IAsyncDisposable
{
    public ApiHost Api;
    public PlainYarpProxy YarpProxy;
    internal SimulatedInternet Internet { get; } = new();

    public YarpFixture()
    {
        Api = new ApiHost(new Uri("https://not-used"), Internet);
        Api.Initialize();

        YarpProxy = new PlainYarpProxy(Api.Url(), Internet);
        YarpProxy.Initialize();
    }


    public HttpClient BuildHttpClient(Uri uri) => Internet.BuildHttpClient(uri);

    public async ValueTask DisposeAsync()
    {
        await Api.DisposeAsync();
        await YarpProxy.DisposeAsync();
    }
}
