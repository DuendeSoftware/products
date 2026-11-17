// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

        // Registered as IConfigureOptions<JwtBearerOptions> (not IPostConfigureOptions<>), and after
        // AddJwtBearer(...), so it runs before ASP.NET Core's own JwtBearerPostConfigureOptions
        // (registered by AddJwtBearer itself). That framework post-configure builds the discovery
        // ConfigurationManager only when Authority or MetadataAddress is already set, so Authority
        // must land here, in a Configure step, not in a PostConfigure step.
        _ = services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ScimJwtBearerConfigureOptions>();

        // Security flags stay in a PostConfigure step, which runs after any host Configure<JwtBearerOptions>
        // call, so a host can't accidentally switch them off.
        _ = services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, ScimJwtBearerPostConfigureOptions>();
    }

    private sealed class ScimJwtBearerConfigureOptions(IOptions<ScimOAuthOptions> scimOptions)
        : IConfigureNamedOptions<JwtBearerOptions>
    {
        public void Configure(string? name, JwtBearerOptions options)
        {
            if (name != ScimConstants.AuthenticationScheme)
            {
                return;
            }

            var scimAuth = scimOptions.Value;

            // Never set an empty string: that would make the framework's post-configure build a
            // ConfigurationManager with no authority to resolve.
            if (!string.IsNullOrWhiteSpace(scimAuth.Authority))
            {
                options.Authority = scimAuth.Authority;
            }

            options.RequireHttpsMetadata = scimAuth.RequireHttpsMetadata;
            options.TokenValidationParameters.ValidAudiences = [scimAuth.Audience];
        }

        public void Configure(JwtBearerOptions options)
        {
            // Unnamed Configure is a no-op: this configure step only applies to the SCIM scheme.
        }
    }

    private sealed class ScimJwtBearerPostConfigureOptions(IServiceProvider serviceProvider)
        : IPostConfigureOptions<JwtBearerOptions>
    {
        public void PostConfigure(string? name, JwtBearerOptions options)
        {
            if (name != ScimConstants.AuthenticationScheme)
            {
                return;
            }

            options.MapInboundClaims = false;
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidateAudience = true;
            options.TokenValidationParameters.ValidateLifetime = true;
            options.TokenValidationParameters.RequireExpirationTime = true;

            // Only when SCIM validates in-process (ConfigurationManager is the local-issuer manager,
            // set by ScimLocalIssuerJwtBearerConfigureOptions, another Configure step that already ran)
            // does an audience alone let an id_token from the same space pass: an id_token's audience
            // is its client id, so it only carries the SCIM audience if a client happens to be named
            // "urn:duende:scim", which the type check below rules out. With an explicit Authority (an
            // external IdP), leave ValidTypes alone: that IdP's token-type conventions are none of
            // SCIM's business.
            if (options.ConfigurationManager is not ScimLocalIssuerConfigurationManager)
            {
                return;
            }

            // IdentityServerOptions may be unavailable when SCIM's module is exercised without
            // IdentityServer (for example in options-only tests): skip the type check rather than throw.
            var identityServerOptions = (serviceProvider.GetService(typeof(IOptions<IdentityServerOptions>))
                as IOptions<IdentityServerOptions>)?.Value;
            if (identityServerOptions is null)
            {
                return;
            }

            // An empty AccessTokenJwtType means those tokens carry no "typ" header at all: a type
            // check would reject every access token, not just id_tokens. "JWT" (the default "typ" for
            // id_tokens too) is set as-is: it still runs, it just gives no protection in that case.
            if (identityServerOptions.AccessTokenJwtType.IsPresent())
            {
                options.TokenValidationParameters.ValidTypes = [identityServerOptions.AccessTokenJwtType];
            }
        }
    }
}

