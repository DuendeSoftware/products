// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;

namespace IdentityServer.UnitTests.Configuration.DependencyInjection;

public class ConfigurationProfileOptionsTests
{
    private const string Category = "ConfigurationProfileOptions";

    [Fact]
    [Trait("Category", Category)]
    public void default_configuration_has_no_profiles()
    {
        var options = new ConfigurationProfileOptions();

        options.EnabledProfiles.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public void default_configuration_enables_logging()
    {
        var options = new ConfigurationProfileOptions();

        options.LogProfileOverrides.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void can_add_fapi2_profile()
    {
        var options = new ConfigurationProfileOptions();

        options.EnabledProfiles.Add(IdentityServerConstants.ConfigurationProfiles.Fapi2);

        options.EnabledProfiles.ShouldContain(IdentityServerConstants.ConfigurationProfiles.Fapi2);
    }

    [Fact]
    [Trait("Category", Category)]
    public void can_add_multiple_profiles()
    {
        var options = new ConfigurationProfileOptions();

        options.EnabledProfiles.Add(IdentityServerConstants.ConfigurationProfiles.Fapi2);
        options.EnabledProfiles.Add("custom-profile");

        options.EnabledProfiles.Count.ShouldBe(2);
        options.EnabledProfiles.ShouldContain(IdentityServerConstants.ConfigurationProfiles.Fapi2);
        options.EnabledProfiles.ShouldContain("custom-profile");
    }

    [Fact]
    [Trait("Category", Category)]
    public void can_disable_logging_of_profile_overrides()
    {
        var options = new ConfigurationProfileOptions
        {
            LogProfileOverrides = false
        };

        options.LogProfileOverrides.ShouldBeFalse();
    }
}
