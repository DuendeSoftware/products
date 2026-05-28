// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Configuration.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace IdentityServer.UnitTests.Licensing;

public class PostConfigureLicenseKeyTests
{
    private static PostConfigureLicenseKey CreateSut(Dictionary<string, string?> configValues) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(configValues).Build());

    [Fact]
    public void should_set_license_key_from_product_specific_config()
    {
        var sut = CreateSut(new()
        {
            ["Duende:IdentityServer:LicenseKey"] = "product-key",
            ["Duende:LicenseKey"] = "shared-key",
        });
        var options = new IdentityServerOptions();

        sut.PostConfigure(null, options);

        options.LicenseKey.ShouldBe("product-key");
    }

    [Fact]
    public void should_fall_back_to_shared_key_when_product_specific_not_set()
    {
        var sut = CreateSut(new()
        {
            ["Duende:LicenseKey"] = "shared-key",
        });
        var options = new IdentityServerOptions();

        sut.PostConfigure(null, options);

        options.LicenseKey.ShouldBe("shared-key");
    }

    [Fact]
    public void should_not_overwrite_imperatively_set_license_key()
    {
        var sut = CreateSut(new()
        {
            ["Duende:IdentityServer:LicenseKey"] = "config-key",
        });
        var options = new IdentityServerOptions { LicenseKey = "imperative-key" };

        sut.PostConfigure(null, options);

        options.LicenseKey.ShouldBe("imperative-key");
    }

    [Fact]
    public void should_leave_license_key_null_when_no_config_key_set()
    {
        var sut = CreateSut(new());
        var options = new IdentityServerOptions();

        sut.PostConfigure(null, options);

        options.LicenseKey.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void should_ignore_empty_or_whitespace_config_values(string emptyValue)
    {
        var sut = CreateSut(new()
        {
            ["Duende:IdentityServer:LicenseKey"] = emptyValue,
            ["Duende:LicenseKey"] = emptyValue,
        });
        var options = new IdentityServerOptions();

        sut.PostConfigure(null, options);

        options.LicenseKey.ShouldBeNull();
    }

    [Fact]
    public void should_trim_license_key_value()
    {
        var sut = CreateSut(new()
        {
            ["Duende:LicenseKey"] = "  some-key  ",
        });
        var options = new IdentityServerOptions();

        sut.PostConfigure(null, options);

        options.LicenseKey.ShouldBe("some-key");
    }
}
