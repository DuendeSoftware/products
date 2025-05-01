// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

/// <summary>
/// Ensures that the <see cref="JwtBearerOptions"/> are configured with <see cref="DPoPJwtBearerEvents"/>.
/// </summary>
internal sealed class ConfigureJwtBearerOptions : IPostConfigureOptions<JwtBearerOptions>
{
    private readonly string _configScheme;

    /// <summary>
    /// Constructs a new instance of <see cref="ConfigureJwtBearerOptions"/> that will operate on the specified scheme name.
    /// </summary>
    public ConfigureJwtBearerOptions(string configScheme) => _configScheme = configScheme;

    /// <inheritdoc/>
    public void PostConfigure(string? name, JwtBearerOptions options)
    {
        if (_configScheme == name)
        {
            if (options.EventsType != null && !typeof(DPoPJwtBearerEvents).IsAssignableFrom(options.EventsType))
            {
                throw new Exception("EventsType on JwtBearerOptions must derive from DPoPJwtBearerEvents to work with the DPoP support.");
            }
            if (options.Events != null && !typeof(DPoPJwtBearerEvents).IsAssignableFrom(options.Events.GetType()))
            {
                throw new Exception("Events on JwtBearerOptions must derive from DPoPJwtBearerEvents to work with the DPoP support.");
            }

            if (options.Events == null && options.EventsType == null)
            {
                options.EventsType = typeof(DPoPJwtBearerEvents);
            }
        }
    }
}
