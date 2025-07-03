// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Tests.TestInfra;
using Xunit.Abstractions;

namespace Duende.Bff.Tests.BffHostBuilder;

public abstract class BffHostBuilderTestBase
{

    protected readonly ITestOutputHelper Output;

    public BffHostBuilderTestBase(ITestOutputHelper output)
    {
        Context = new TestHostContext(output);
        Output = output;

        IdentityServer = new IdentityServerTestHost(Context);
        Api = new ApiHost(Context, IdentityServer);
        Cdn = new CdnHost(Context);
        The.Authority = IdentityServer.Url();
    }

    public TestData The => Context.The;
    public TestDataBuilder Some => new TestDataBuilder(The);
    public TestHostContext Context { get; set; }

    public ApiHost Api { get; set; }
    public CdnHost Cdn { get; set; }
    public IdentityServerTestHost IdentityServer { get; }


    protected async Task<BffHttpClient> InitializeAsync(IHost app)
    {
        await app.StartAsync();

        await Api.InitializeAsync();
        await IdentityServer.InitializeAsync();
        await Cdn.InitializeAsync();

        Context.Internet.AddHandler(IdentityServer);
        Context.Internet.AddHandler(Api);
        Context.Internet.AddHandler(Cdn);

        IdentityServer.AddClient(The.ClientId, app.GetBffUri());

        var cookieContainer = new CookieContainer();
        var cookieHandler = new CookieHandler(Context.Internet, cookieContainer);
        var redirectHandler = new RedirectHandler(Context.WriteOutput)
        {
            InnerHandler = cookieHandler
        };

        Context.Internet.AddHandler(app.GetBffUri(), app.GetTestHandler());

        return new BffHttpClient(redirectHandler, cookieContainer, IdentityServer)
        {
            BaseAddress = app.GetBffUri()
        };
    }

    protected void AdvanceClock(TimeSpan by) => The.Clock.SetUtcNow(The.Clock.GetUtcNow().Add(by));
}
