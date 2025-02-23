// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting;
using Duende.IdentityServer.Extensions;
using Xunit;

namespace UnitTests.Extensions;

public class EndpointOptionsExtensionsTests
{
    private readonly EndpointsOptions _options = new EndpointsOptions();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForAuthorizeEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableAuthorizeEndpoint = expectedIsEndpointEnabled;

        Assert.Equal(
            expectedIsEndpointEnabled,
            _options.IsEndpointEnabled(
                CreateTestEndpoint(IdentityServerConstants.EndpointNames.Authorize)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForCheckSessionEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableCheckSessionEndpoint = expectedIsEndpointEnabled;

        Assert.Equal(
            expectedIsEndpointEnabled,
            _options.IsEndpointEnabled(
                CreateTestEndpoint(IdentityServerConstants.EndpointNames.CheckSession)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForDeviceAuthorizationEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableDeviceAuthorizationEndpoint = expectedIsEndpointEnabled;

        Assert.Equal(
            expectedIsEndpointEnabled,
            _options.IsEndpointEnabled(
                CreateTestEndpoint(IdentityServerConstants.EndpointNames.DeviceAuthorization)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForDiscoveryEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableDiscoveryEndpoint = expectedIsEndpointEnabled;

        Assert.Equal(
            expectedIsEndpointEnabled,
            _options.IsEndpointEnabled(
                CreateTestEndpoint(IdentityServerConstants.EndpointNames.Discovery)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForEndSessionEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableEndSessionEndpoint = expectedIsEndpointEnabled;

        Assert.Equal(
            expectedIsEndpointEnabled,
            _options.IsEndpointEnabled(
                CreateTestEndpoint(IdentityServerConstants.EndpointNames.EndSession)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForIntrospectionEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableIntrospectionEndpoint = expectedIsEndpointEnabled;

        Assert.Equal(
            expectedIsEndpointEnabled,
            _options.IsEndpointEnabled(
                CreateTestEndpoint(IdentityServerConstants.EndpointNames.Introspection)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForTokenEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableTokenEndpoint = expectedIsEndpointEnabled;

        Assert.Equal(
            expectedIsEndpointEnabled,
            _options.IsEndpointEnabled(
                CreateTestEndpoint(IdentityServerConstants.EndpointNames.Token)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForRevocationEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableTokenRevocationEndpoint = expectedIsEndpointEnabled;

        Assert.Equal(
            expectedIsEndpointEnabled,
            _options.IsEndpointEnabled(
                CreateTestEndpoint(IdentityServerConstants.EndpointNames.Revocation)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForUserInfoEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableUserInfoEndpoint = expectedIsEndpointEnabled;

        Assert.Equal(
            expectedIsEndpointEnabled,
            _options.IsEndpointEnabled(
                CreateTestEndpoint(IdentityServerConstants.EndpointNames.UserInfo)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForPushedAuthorizationEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnablePushedAuthorizationEndpoint = expectedIsEndpointEnabled;

        Assert.Equal(
            expectedIsEndpointEnabled,
            _options.IsEndpointEnabled(
                CreateTestEndpoint(IdentityServerConstants.EndpointNames.PushedAuthorization)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForBackchannelAuthenticationEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableBackchannelAuthenticationEndpoint = expectedIsEndpointEnabled;

        Assert.Equal(
            expectedIsEndpointEnabled,
            _options.IsEndpointEnabled(
                CreateTestEndpoint(IdentityServerConstants.EndpointNames.BackchannelAuthentication)));
    }    

    private Endpoint CreateTestEndpoint(string name)
    {
        return new Endpoint(name, "", null);
    }
}