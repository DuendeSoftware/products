// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Licensing;
using Duende.UserManagement.Internal.Modules;
using Duende.UserManagement.Internal.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement.Internal;

internal sealed class UserCoreModule : IDuendeModule
{
    public static void Register(IServiceCollection services)
    {
        services.RegisterModule<StorageModule>();
        services.RegisterModule<LicensingModule>();

        // 1. Register DSO types
        services.RegisterDsoType<UserDso.V1>();

        // 2. Register repositories
        _ = services.AddScoped<UserRepository>();
    }
}
