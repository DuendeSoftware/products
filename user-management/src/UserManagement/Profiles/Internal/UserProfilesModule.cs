// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.Internal.Builder;
using Duende.UserManagement.Import.Internal;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Internal.Modules;
using Duende.UserManagement.Membership.Internal;
using Duende.UserManagement.Profiles.Internal.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.UserManagement.Profiles.Internal;

internal sealed class UserProfilesModule : IDuendeModule
{
    public static void Register(IServiceCollection services)
    {
        services.RegisterModule<UserCoreModule>();
        services.RegisterModule<UserMembershipModule>();
        services.RegisterModule<UserImportModule>();

        services.RegisterFeature<UserProfilesFeature>();

        // 1. Register DSO types
        services.AddDsoRegistration<UserProfileDso.V1>();
        services.AddDynamicSchemaStorage();

        // 2. Register self-service
        _ = services.AddTransient<IUserProfileSelfService, UserProfileSelfService>();
        services.TryAddTransient<IUserSelfService, UserSelfService>();

        // 3. Register admin services
        _ = services.AddTransient<IUserProfileAdmin, UserProfileAdmin>();
        services.TryAddTransient<IUserAdmin, UserAdmin>();

        // 4. Register misc services
        services.TryAddSingleton<TimeProvider>(_ => TimeProvider.System);

        // 5. Register repositories
        _ = services.AddScoped<UserProfileRepository>();

        // 6. Register readers
        _ = services.AddScoped<UserProfileReader>();
    }
}
