// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Duende.IdentityServer.IntegrationTests.Common;

namespace Duende.IdentityServer.IntegrationTests.Endpoints.Discovery;

public class DiscoveryEndpointTests_introspection_endpoint_auth_methods_supported : DiscoveryEndpointTestsBase
{
    private const string Category = "Discovery endpoint - introspection_endpoint_auth_methods_supported";

    [Fact]
    [Trait("Category", Category)]
    public async Task introspection_endpoint_auth_methods_supported_should_default_to_basic_auth_and_post_body()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();
        pipeline.Options.MutualTls.Enabled = false;

        var disco = await pipeline.BackChannelClient
            .GetDiscoveryDocumentAsync("https://server/.well-known/openid-configuration");
        disco.IsError.ShouldBeFalse();

        var authMethodsSupported = disco.IntrospectionEndpointAuthenticationMethodsSupported;

        authMethodsSupported.Count().ShouldBe(2);
        authMethodsSupported.ShouldContain(OidcConstants.EndpointAuthenticationMethods.BasicAuthentication);
        authMethodsSupported.ShouldContain(OidcConstants.EndpointAuthenticationMethods.PostBody);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task introspection_endpoint_auth_methods_supported_should_include_tls_client_and_self_signed_tls_client_when_mtls_is_enabled()
    {
        var pipeline = new IdentityServerPipeline();
        pipeline.Initialize();
        pipeline.Options.MutualTls.Enabled = true;

        var disco = await pipeline.BackChannelClient
            .GetDiscoveryDocumentAsync("https://server/.well-known/openid-configuration");
        disco.IsError.ShouldBeFalse();

        var authMethodsSupported = disco.IntrospectionEndpointAuthenticationMethodsSupported;

        authMethodsSupported.Count().ShouldBe(4);
        authMethodsSupported.ShouldContain(OidcConstants.EndpointAuthenticationMethods.BasicAuthentication);
        authMethodsSupported.ShouldContain(OidcConstants.EndpointAuthenticationMethods.PostBody);
        authMethodsSupported.ShouldContain(OidcConstants.EndpointAuthenticationMethods.TlsClientAuth);
        authMethodsSupported.ShouldContain(OidcConstants.EndpointAuthenticationMethods.SelfSignedTlsClientAuth);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task introspection_endpoint_auth_methods_supported_should_include_private_key_jwt_when_jwt_bearer_is_enabled()
    {
        var pipeline = CreatePipelineWithJwtBearer();
        pipeline.Initialize();
        pipeline.Options.MutualTls.Enabled = false;

        var disco = await pipeline.BackChannelClient
            .GetDiscoveryDocumentAsync("https://server/.well-known/openid-configuration");
        disco.IsError.ShouldBeFalse();

        var authMethodsSupported = disco.IntrospectionEndpointAuthenticationMethodsSupported;

        authMethodsSupported.Count().ShouldBe(3);
        authMethodsSupported.ShouldContain(OidcConstants.EndpointAuthenticationMethods.BasicAuthentication);
        authMethodsSupported.ShouldContain(OidcConstants.EndpointAuthenticationMethods.PostBody);
        authMethodsSupported.ShouldContain(OidcConstants.EndpointAuthenticationMethods.PrivateKeyJwt);
    }
}

