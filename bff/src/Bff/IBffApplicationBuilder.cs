// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.Bff.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Duende.Bff;

public interface IBffApplicationBuilder
{
    internal IServiceCollection InternalServices { get; }
    internal IList<IBffPartBuilder> Parts { get; }

    internal List<ConfigureWebAppBuilder> WebAppConfigurators { get; }

    IHostApplicationBuilder HostBuilder { get; }

    internal bool TryGetPart<T>([NotNullWhen(true)] out T? part) where T : IBffPartBuilder
    {
        part = Parts.OfType<T>().FirstOrDefault();
        return part != null;
    }

    IBffApplicationBuilder UsingServiceDefaults(Action<BffApplicationPartType, IHostApplicationBuilder> configure);
}
