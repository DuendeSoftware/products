// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

#pragma warning disable duende_experimental

namespace Duende.UserManagement.Scim.Internal;

/// <summary>
/// Registers JWT bearer authentication for the SCIM scheme.
/// </summary>
internal sealed class ScimOAuthModule
{
    public static void Register(IServiceCollection services)
    {
        _ = services.AddAuthentication()
            .AddJwtBearer(ScimConstants.AuthenticationScheme, _ => { });

        _ = services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, ScimJwtBearerPostConfigureOptions>();
    }

    private sealed class ScimJwtBearerPostConfigureOptions(IOptions<ScimOAuthOptions> scimOptions)
        : IPostConfigureOptions<JwtBearerOptions>
    {
        public void PostConfigure(string? name, JwtBearerOptions options)
        {
            if (name != ScimConstants.AuthenticationScheme)
            {
                return;
            }

            var scimAuth = scimOptions.Value;

            options.Authority = scimAuth.Authority;
            options.RequireHttpsMetadata = scimAuth.RequireHttpsMetadata;
            options.MapInboundClaims = false;
            options.TokenValidationParameters.ValidateAudience = true;
            options.TokenValidationParameters.ValidAudiences = [scimAuth.Audience];
            options.TokenValidationParameters.RequireExpirationTime = true;
            options.TokenValidationParameters.ValidateLifetime = true;
            options.TokenValidationParameters.ValidateIssuer = true;
        }
    }
}

#pragma warning restore duende_experimental
