// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Duende.UserManagement.Authentication.Internal;
using Duende.UserManagement.Internal.Licensing;
using Duende.UserManagement.Scim.Internal;
using Duende.UserManagement.Scim.Internal.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.UserManagement;

/// <summary>
/// Extension methods for mapping user management endpoints to an <see cref="IEndpointRouteBuilder"/>.
/// </summary>
public static class UserManagementEndpointBuilderExtensions
{
    extension<T>(T builder) where T : IEndpointRouteBuilder
    {
        /// <summary>
        /// Maps the user management authentication endpoints.
        /// </summary>
        /// <returns>The endpoint route builder for chaining.</returns>
        public T MapUserManagement()
        {
            builder.ServiceProvider.GetRequiredService<UserAuthenticationWebModule>().MapEndpoints(builder);
            return builder;
        }

        /// <summary>
        /// Maps the SCIM endpoints.
        /// </summary>
        /// <returns>The endpoint route builder for chaining.</returns>
        [Experimental(diagnosticId: "duende_experimental", Message = "SCIM support is experimental and may change in future releases.")]
        public T MapScim()
        {
            var licenseValidator = builder.ServiceProvider.GetRequiredService<UserManagementLicenseValidator>();
            if (!licenseValidator.ValidateInboundScim())
            {
                UserManagementLicenseValidator.ThrowInvalidLicenseException("Your license does not include the Inbound SCIM feature.");
            }

            builder.ServiceProvider.GetRequiredService<ScimMetadataModule>().MapEndpoints(builder);
            builder.ServiceProvider.GetRequiredService<ScimUsersHttpModule>().MapEndpoints(builder);
            builder.ServiceProvider.GetRequiredService<ScimGroupsModule>().MapEndpoints(builder);
            builder.ServiceProvider.GetRequiredService<ScimBulkModule>().MapEndpoints(builder);
            return builder;
        }
    }
}
