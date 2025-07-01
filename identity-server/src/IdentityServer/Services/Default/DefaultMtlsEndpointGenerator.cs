// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Services.Default;

internal class DefaultMtlsEndpointGenerator(IServerUrls serverUrls, IOptions<IdentityServerOptions> options)
    : IMtlsEndpointGenerator
{
    //Note: This logic is currently duplicated in the DiscoveryResponseGenerator as adding a new
    //dependency there would be a breaking change in a non-major release.
    public string GetMtlsEndpointPath(string endpoint)
    {
        var baseUrl = serverUrls.BaseUrl.EnsureTrailingSlash();

        // path based
        if (options.Value.MutualTls.DomainName.IsMissing())
        {
            return baseUrl + endpoint.Replace(IdentityServerConstants.ProtocolRoutePaths.ConnectPathPrefix, IdentityServerConstants.ProtocolRoutePaths.MtlsPathPrefix);
        }

        // domain based
        if (options.Value.MutualTls.DomainName.Contains('.'))
        {
            return $"https://{options.Value.MutualTls.DomainName}/{endpoint}";
        }
        // sub-domain based
        else
        {
            var parts = baseUrl.Split("://");
            return $"https://{options.Value.MutualTls.DomainName}.{parts[1]}{endpoint}";
        }
    }
}
