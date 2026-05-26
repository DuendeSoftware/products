// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.UserManagement.Authentication.Internal;
using Duende.UserManagement.Scim.Internal;
using Duende.UserManagement.Scim.Internal.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement;

public static class UserManagementEndpointBuilderExtensions
{
    extension<T>(T builder) where T : IEndpointRouteBuilder
    {
        public T MapUserManagement()
        {
            builder.ServiceProvider.GetRequiredService<UserAuthenticationWebModule>().MapEndpoints(builder);
            return builder;
        }

        [Experimental(diagnosticId: "duende_experimental", Message = "SCIM support is experimental and may change in future releases.")]
        public T MapScim()
        {
            builder.ServiceProvider.GetRequiredService<ScimMetadataModule>().MapEndpoints(builder);
            builder.ServiceProvider.GetRequiredService<ScimUsersHttpModule>().MapEndpoints(builder);
            builder.ServiceProvider.GetRequiredService<ScimGroupsModule>().MapEndpoints(builder);
            builder.ServiceProvider.GetRequiredService<ScimBulkModule>().MapEndpoints(builder);
            return builder;
        }
    }
}
