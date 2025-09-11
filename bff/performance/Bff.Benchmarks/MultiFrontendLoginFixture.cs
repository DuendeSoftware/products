// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Bff.Benchmarks.Hosts;
using Duende.Bff.DynamicFrontends;

namespace Bff.Benchmarks;

public class MultiFrontendLoginFixture : IAsyncDisposable
{
    public ApiHost Api;
    public IdentityServerHost IdentityServer;
    public BffHost Bff;

    internal SimulatedInternet Internet { get; } = new();

    public MultiFrontendLoginFixture()
    {
        IdentityServer = new IdentityServerHost(Internet);
        IdentityServer.Initialize();

        Api = new ApiHost(IdentityServer.Url(), Internet);
        Api.Initialize();

        Bff = new BffHost(new Uri("https://bff"), IdentityServer.Url(), Api.Url(), Internet);
        Bff.Initialize();

        List<Uri> bffUrls = [Bff.Url()];
        for (var i = 0; i < 500; i++)
        {
            Bff.AddFrontend(new Uri($"https://frontend{i}.example.com/"));
        }
        for (var i = 0; i < 500; i++)
        {
            var path = BenchmarkBase.GetPath(i);
            Bff.AddFrontend(path);
            var item = Bff.Url(path + "/");
            bffUrls.Add(item);
        }

        Bff.AddFrontend(BffFrontendName.Parse("default"));

        IdentityServer.AddClient(bffUrls);
    }

    public async ValueTask DisposeAsync()
    {
        await IdentityServer.DisposeAsync();
        await Api.DisposeAsync();
        await Bff.DisposeAsync();
    }

}
