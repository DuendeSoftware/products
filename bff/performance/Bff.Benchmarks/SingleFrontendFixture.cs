// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Bff.Benchmarks.Hosts;

namespace Bff.Benchmarks;

public class SingleFrontendFixture : IAsyncDisposable
{
    public ApiHost Api;
    public IdentityServerHost IdentityServer;
    public BffHost Bff;
    internal SimulatedInternet Internet { get; } = new();

    public SingleFrontendFixture()
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
            var path = BenchmarkBase.GetPath(i);
            //   Bff.AddFrontend(LocalPath.Parse(path));
            var item = Bff.Url(path + "/");
            bffUrls.Add(item);
        }
        IdentityServer.AddClient(bffUrls);
    }


    public async ValueTask DisposeAsync()
    {
        await IdentityServer.DisposeAsync();
        await Api.DisposeAsync();
        await Bff.DisposeAsync();
    }
}
