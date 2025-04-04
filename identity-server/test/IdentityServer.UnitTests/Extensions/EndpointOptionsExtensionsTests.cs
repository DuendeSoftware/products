// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Hosting;

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
        var actual = _options.IsEndpointEnabled(CreateTestEndpoint(IdentityServerConstants.EndpointNames.Authorize));

        actual.ShouldBe(expectedIsEndpointEnabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForCheckSessionEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableCheckSessionEndpoint = expectedIsEndpointEnabled;
        var actual = _options.IsEndpointEnabled(CreateTestEndpoint(IdentityServerConstants.EndpointNames.CheckSession));

        actual.ShouldBe(expectedIsEndpointEnabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForDeviceAuthorizationEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableDeviceAuthorizationEndpoint = expectedIsEndpointEnabled;
        var actual = _options.IsEndpointEnabled(CreateTestEndpoint(IdentityServerConstants.EndpointNames.DeviceAuthorization));

        actual.ShouldBe(expectedIsEndpointEnabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForDiscoveryEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableDiscoveryEndpoint = expectedIsEndpointEnabled;
        var actual = _options.IsEndpointEnabled(CreateTestEndpoint(IdentityServerConstants.EndpointNames.Discovery));

        actual.ShouldBe(expectedIsEndpointEnabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForEndSessionEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableEndSessionEndpoint = expectedIsEndpointEnabled;
        var actual = _options.IsEndpointEnabled(CreateTestEndpoint(IdentityServerConstants.EndpointNames.EndSession));

        actual.ShouldBe(expectedIsEndpointEnabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForIntrospectionEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableIntrospectionEndpoint = expectedIsEndpointEnabled;
        var actual = _options.IsEndpointEnabled(CreateTestEndpoint(IdentityServerConstants.EndpointNames.Introspection));
        actual.ShouldBe(expectedIsEndpointEnabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForTokenEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableTokenEndpoint = expectedIsEndpointEnabled;
        var actual = _options.IsEndpointEnabled(CreateTestEndpoint(IdentityServerConstants.EndpointNames.Token));

        actual.ShouldBe(expectedIsEndpointEnabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForRevocationEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableTokenRevocationEndpoint = expectedIsEndpointEnabled;
        var actual = _options.IsEndpointEnabled(CreateTestEndpoint(IdentityServerConstants.EndpointNames.Revocation));

        actual.ShouldBe(expectedIsEndpointEnabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForUserInfoEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableUserInfoEndpoint = expectedIsEndpointEnabled;
        var actual = _options.IsEndpointEnabled(CreateTestEndpoint(IdentityServerConstants.EndpointNames.UserInfo));

        actual.ShouldBe(expectedIsEndpointEnabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForPushedAuthorizationEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnablePushedAuthorizationEndpoint = expectedIsEndpointEnabled;
        var actual = _options.IsEndpointEnabled(CreateTestEndpoint(IdentityServerConstants.EndpointNames.PushedAuthorization));
        actual.ShouldBe(expectedIsEndpointEnabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsEndpointEnabledShouldReturnExpectedForBackchannelAuthenticationEndpoint(bool expectedIsEndpointEnabled)
    {
        _options.EnableBackchannelAuthenticationEndpoint = expectedIsEndpointEnabled;
        var actual = _options.IsEndpointEnabled(CreateTestEndpoint(IdentityServerConstants.EndpointNames.BackchannelAuthentication));

        actual.ShouldBe(expectedIsEndpointEnabled);
    }

    private Endpoint CreateTestEndpoint(string name) => new Endpoint(name, "", null);
}
