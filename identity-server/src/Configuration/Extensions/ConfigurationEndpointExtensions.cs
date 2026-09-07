// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Extension methods for adding IdentityServer configuration endpoints.
/// </summary>
public static class ConfigurationEndpointExtensions
{
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps the dynamic client registration endpoint.
        /// </summary>
        public IEndpointConventionBuilder MapDynamicClientRegistration(string path = "/connect/dcr")
        {
            endpoints.CheckLicense();

            return endpoints.MapPost(path, (DynamicClientRegistrationEndpoint endpoint, HttpContext context) => endpoint.Process(context));
        }

        internal void CheckLicense()
        {
            var licenseValidator = endpoints.ServiceProvider.GetRequiredService<IdentityServerConfigurationLicenseValidator>();
            licenseValidator.ValidateDynamicClientRegistration();
        }
    }
}
