// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;
using Duende.Bff.DynamicFrontends;
using Microsoft.Extensions.Options;

namespace Hosts.Bff.Performance.Services;

public class MultiFrontendBffService(IConfiguration config, IOptions<BffSettings> settings)
    : BffService(["BffUrl2", "BffUrl3"], config, settings)
{
    private readonly IConfiguration _config = config;

    public override void ConfigureServices(IServiceCollection services)
    {
    }

    public override void ConfigureBff(BffBuilder bff) => bff
        .WithDefaultOpenIdConnectOptions(o => DefaultOpenIdConfiguration.Apply(o, Settings))
        .AddFrontends(new BffFrontend(BffFrontendName.Parse("default")))

        // Note, in order for this to work, we'll need to inject this as config
        .AddFrontends(
            new BffFrontend(BffFrontendName.Parse("app1")).MappedToOrigin(_config.GetValue<Origin>("BffUrl3")!));

    public override void ConfigureApp(WebApplication app) => app.MapGet("/",
        (SelectedFrontend selectedFrontend) => "multi - " + selectedFrontend.Get().Name);
}
