// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Scim.Internal;

/// <summary>
/// Gives the SCIM bearer scheme (<see cref="ScimConstants.AuthenticationScheme"/>) an in-process
/// <see cref="JwtBearerOptions.ConfigurationManager"/> when <see cref="ScimOAuthOptions.Authority"/>
/// is not set. Registered as <see cref="IConfigureOptions{TOptions}"/>, so it runs alongside SCIM's
/// own connection-settings configure step (<c>ScimOAuthModule.ScimJwtBearerConfigureOptions</c>),
/// before ASP.NET Core's framework post-configure. When <c>Authority</c> is set, this leaves
/// <see cref="JwtBearerOptions.ConfigurationManager"/> alone, so the framework builds its own
/// discovery-based manager from <c>Authority</c>, unchanged from today.
/// </summary>
#pragma warning disable CA1812 // Instantiated via DI
internal sealed class ScimLocalIssuerJwtBearerConfigureOptions(
    IOptions<ScimOAuthOptions> scimOptions,
    ScimLocalIssuerConfigurationManager configurationManager)
#pragma warning restore CA1812
    : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != ScimConstants.AuthenticationScheme)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(scimOptions.Value.Authority))
        {
            return;
        }

        if (options.ConfigurationManager is not null)
        {
            return;
        }

        options.ConfigurationManager = configurationManager;
    }

    public void Configure(JwtBearerOptions options)
    {
        // Unnamed Configure is a no-op: this configure step only applies to the SCIM scheme.
    }
}
