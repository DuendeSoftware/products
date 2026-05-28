// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Private.Licencing.V2;
using Duende.UserManagement.Internal.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Internal.Licensing;

internal sealed class LicensingModule : IDuendeModule
{
    public static void Register(IServiceCollection services)
    {
        services.TryAddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<V2LicenseAccessor>>();
            var accessor = new V2LicenseAccessor(() => null, logger);
            return accessor.Current;
        });

        services.TryAddSingleton<LicenseValidator>();
        services.TryAddSingleton<UserManagementLicenseValidator>();
    }
}
