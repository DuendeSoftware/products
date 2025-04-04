// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting;

namespace Duende.IdentityServer.Extensions;

internal static class EndpointOptionsExtensions
{
    public static bool IsEndpointEnabled(this EndpointsOptions options, Endpoint endpoint) => endpoint?.Name switch
    {
        IdentityServerConstants.EndpointNames.Authorize => options.EnableAuthorizeEndpoint,
        IdentityServerConstants.EndpointNames.CheckSession => options.EnableCheckSessionEndpoint,
        IdentityServerConstants.EndpointNames.DeviceAuthorization => options.EnableDeviceAuthorizationEndpoint,
        IdentityServerConstants.EndpointNames.Discovery => options.EnableDiscoveryEndpoint,
        IdentityServerConstants.EndpointNames.EndSession => options.EnableEndSessionEndpoint,
        IdentityServerConstants.EndpointNames.Introspection => options.EnableIntrospectionEndpoint,
        IdentityServerConstants.EndpointNames.Revocation => options.EnableTokenRevocationEndpoint,
        IdentityServerConstants.EndpointNames.Token => options.EnableTokenEndpoint,
        IdentityServerConstants.EndpointNames.UserInfo => options.EnableUserInfoEndpoint,
        IdentityServerConstants.EndpointNames.PushedAuthorization => options.EnablePushedAuthorizationEndpoint,
        IdentityServerConstants.EndpointNames.BackchannelAuthentication => options.EnableBackchannelAuthenticationEndpoint,
        _ => true
    };
}
