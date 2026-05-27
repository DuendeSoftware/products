// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Licensing.Enforcement;
using Duende.UserManagement.Internal.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.UserManagement.Internal.Licensing;

internal sealed class LicensingModule : IDuendeModule
{
    public static void Register(IServiceCollection services)
    {
        _ = services.AddOptions<LicenseOptions>();
        _ = services.AddDuendeLicensing();
        services.TryAddSingleton<UserManagementLicenseValidator>();
    }
}
