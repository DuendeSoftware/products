// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;
using Duende.Bff.DynamicFrontends;

namespace Hosts.Bff.Performance.Services;

public class MultiFrontendBffService() : BffService(new BffServiceSettings()
{
    Uri = "https://*:6002"
})
{
    public override void ConfigureServices(IServiceCollection services)
    {
    }

    public override void ConfigureBff(BffBuilder bff) => bff.WithDefaultOpenIdConnectOptions(DefaultOpenIdConfiguration.Apply)
        .AddFrontends(new BffFrontend(BffFrontendName.Parse("default")))

        // Note, in order for this to work, we'll need to inject this as config
        .AddFrontends(new BffFrontend(BffFrontendName.Parse("app1")).MappedToOrigin(Origin.Parse("https://app1.localhost:6002")));

    public override void ConfigureApp(WebApplication app) => app.MapGet("/", (SelectedFrontend selectedFrontend) => "multi - " + selectedFrontend.Get().Name);
}
