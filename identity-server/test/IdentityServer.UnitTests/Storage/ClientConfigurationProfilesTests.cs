// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;

namespace IdentityServer.UnitTests.Storage;

public class ClientConfigurationProfilesTests
{
    private const string Category = "Client Configuration Profiles";

    [Fact]
    [Trait("Category", Category)]
    public void default_client_has_no_configuration_profiles()
    {
        var client = new Client();

        client.ConfigurationProfiles.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Category", Category)]
    public void can_add_fapi2_profile_to_client()
    {
        var client = new Client
        {
            ClientId = "test",
            ConfigurationProfiles = { ConfigurationProfiles.Fapi2 }
        };

        client.ConfigurationProfiles.ShouldContain(ConfigurationProfiles.Fapi2);
    }

    [Fact]
    [Trait("Category", Category)]
    public void can_add_multiple_profiles_to_client()
    {
        var client = new Client
        {
            ClientId = "test"
        };

        client.ConfigurationProfiles.Add(ConfigurationProfiles.Fapi2);
        client.ConfigurationProfiles.Add("custom-profile");

        client.ConfigurationProfiles.Count.ShouldBe(2);
        client.ConfigurationProfiles.ShouldContain(ConfigurationProfiles.Fapi2);
        client.ConfigurationProfiles.ShouldContain("custom-profile");
    }

    [Fact]
    [Trait("Category", Category)]
    public void can_replace_configuration_profiles_collection()
    {
        var client = new Client
        {
            ClientId = "test",
            ConfigurationProfiles = new HashSet<string> { ConfigurationProfiles.Fapi2, "custom-profile" }
        };

        client.ConfigurationProfiles.Count.ShouldBe(2);
        client.ConfigurationProfiles.ShouldContain(ConfigurationProfiles.Fapi2);
        client.ConfigurationProfiles.ShouldContain("custom-profile");
    }
}
