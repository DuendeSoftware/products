// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Modules;
using Duende.UserManagement.Internal.Services;
using Duende.UserManagement.Scim.Internal.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable duende_experimental

namespace Duende.UserManagement.Scim.Internal;

internal sealed class ScimModule : IDuendeModule
{
    public static void Register(IServiceCollection services)
    {
        _ = services.AddHttpContextAccessor();
        _ = services.AddTransient<IServerUrls, DefaultServerUrls>();
        services.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);

        // Authentication and authorization — always active for SCIM endpoints
        ScimOAuthModule.Register(services);
        ScimAuthorizationPolicyProvider.Register(services);

        services.RegisterModule<ScimUsersHttpModule>();
        services.RegisterModule<ScimGroupsModule>();
        services.RegisterModule<ScimBulkModule>();
    }
}

#pragma warning restore duende_experimental
