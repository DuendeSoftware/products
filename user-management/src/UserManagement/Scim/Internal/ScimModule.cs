// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Modules;
using Duende.UserManagement.Internal.Services;
using Duende.UserManagement.Scim.Internal.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.UserManagement.Scim.Internal;

internal sealed class ScimModule : IDuendeModule
{
    public static void Register(IServiceCollection services)
    {
        _ = services.AddHttpContextAccessor();
        _ = services.AddTransient<IServerUrls, DefaultServerUrls>();
        services.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);

        services.RegisterModule<ScimUsersHttpModule>();
        services.RegisterModule<ScimGroupsModule>();
        services.RegisterModule<ScimBulkModule>();
    }
}
