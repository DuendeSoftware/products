// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Builder;
using Duende.Bff.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff;

public interface IBffEndpointBuilder : IBffBuilder, IBffPartBuilder
{
    IBffEndpointBuilder ConfigureApp(Action<WebApplication> configure);

    IBffEndpointBuilder ConfigureOptions(Action<BffOptions> configure)
    {
        Services.Configure<BffOptions>(configure);
        return this;
    }

    IBffEndpointBuilder UseUrls(params string[] urls);

}
