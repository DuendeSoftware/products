// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Duende.UserManagement.Import.Internal;

internal sealed class UserImportModule : IDuendeModule
{
    public static void Register(IServiceCollection services)
    {
        _ = services.AddTransient<IUserImporter, UserImporter>();
        services.TryAddSingleton<IUserImportConflictResolver, DefaultUserImportConflictResolver>();
    }
}
