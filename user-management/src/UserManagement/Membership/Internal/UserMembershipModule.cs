// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Builder;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Internal.Modules;
using Duende.UserManagement.Internal.Storage;
using Duende.UserManagement.Membership.Internal.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement.Membership.Internal;

internal sealed class UserMembershipModule : IDuendeModule
{
    public static void Register(IServiceCollection services)
    {
        services.RegisterModule<UserCoreModule>();
        services.RegisterModule<StorageModule>();
        services.RegisterFeature<MembershipFeature>();

        // 1. Register DSO types
        services.AddDsoRegistration<RoleDso.V1>();
        services.AddDsoRegistration<GroupDso.V1>();

        // 2. Register admin services
        _ = services.AddTransient<IMembershipAdmin, MembershipAdmin>();
        _ = services.AddTransient<IRoleAdmin, RoleAdmin>();
        _ = services.AddTransient<IGroupAdmin, GroupAdmin>();


        // 3. Register repositories
        _ = services.AddScoped<MembershipRepository>();
        _ = services.AddScoped<RoleRepository>();
        _ = services.AddScoped<GroupRepository>();
    }
}
