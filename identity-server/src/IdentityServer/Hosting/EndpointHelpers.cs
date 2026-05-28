// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Hosting;

internal static class EndpointHelpers
{
    public static class OAuthMetadataHelpers
    {
        public static bool IsMatch(HttpContext httpContext) => httpContext.Request.Path.StartsWithSegments("/.well-known/oauth-authorization-server",
                StringComparison.OrdinalIgnoreCase);
    }

    public static class SamlMetadataHelpers
    {
        /// <summary>
        /// Matches requests to the SAML metadata endpoint. Per SAML Metadata §4.1.1,
        /// the metadata document should be available at the entity ID URL. The path
        /// is resolved from <see cref="SamlOptions"/> at request time.
        /// </summary>
        public static bool IsMatch(HttpContext httpContext)
        {
            var options = httpContext.RequestServices.GetRequiredService<IOptions<IdentityServerOptions>>().Value;
            var path = ResolveMetadataPath(options.Saml);
            return httpContext.Request.Path.Equals(path, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves the metadata endpoint path from <see cref="SamlOptions"/>.
        /// If the entity ID is an HTTP/HTTPS URL, its path component is used.
        /// Otherwise, falls back to <see cref="SamlOptions.EntityIdPath"/>.
        /// </summary>
        internal static string ResolveMetadataPath(SamlOptions options)
        {
            if (options.EntityId is { } entityId &&
                Uri.TryCreate(entityId, UriKind.Absolute, out var uri) &&
                uri.Scheme is "http" or "https" &&
                uri.AbsolutePath is not "/")
            {
                return uri.AbsolutePath;
            }

            return options.EntityIdPath;
        }
    }
}
