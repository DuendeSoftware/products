// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;
using Duende.Bff.DynamicFrontends;

namespace Hosts.Bff.Performance.Services;

public class SingleFrontendBffService() : BffService(new BffServiceSettings()
{
    Uri = "https://localhost:6001"
})
{
    public override void ConfigureServices(IServiceCollection services)
    {
    }

    public override void ConfigureBff(BffBuilder bff) => bff.WithDefaultOpenIdConnectOptions(DefaultOpenIdConfiguration.Apply)
        .AddFrontends(new BffFrontend(BffFrontendName.Parse("default")));

    public override void ConfigureApp(WebApplication app) => app.MapGet("/", () => "single");
}
