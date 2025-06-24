// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff;
using Duende.Bff.DynamicFrontends;
using Microsoft.Extensions.Options;

namespace Hosts.Bff.Performance.Services;

public class SingleFrontendBffService(IConfiguration config, IOptions<BffSettings> settings) : BffService(["BffUrl1"], config, settings)
{
    public override void ConfigureServices(IServiceCollection services)
    {
    }

    public override void ConfigureBff(BffBuilder bff) => bff.WithDefaultOpenIdConnectOptions(o => DefaultOpenIdConfiguration.Apply(o, Settings))
        .AddFrontends(new BffFrontend(BffFrontendName.Parse("default")));

    public override void ConfigureApp(WebApplication app) => app.MapGet("/", () => "single");
}
