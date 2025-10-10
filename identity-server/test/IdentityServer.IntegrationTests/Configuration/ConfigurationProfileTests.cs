// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Configuration.Profiles;
using Duende.IdentityServer.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityServer.IntegrationTests.Configuration;

public class ConfigurationProfileTests
{
    private const string Category = "Configuration Profiles";

    [Fact]
    [Trait("Category", Category)]
    public void profile_should_be_invoked_when_registered()
    {
        var profileInvoked = false;

        var builder = WebApplication.CreateBuilder();

        var profileService = new TestProfileService("test-profile", options => profileInvoked = true);

        builder.Services.AddIdentityServer(options =>
        {
            options.ConfigurationProfiles.EnabledProfiles.Add("test-profile");
        });
        builder.Services.AddSingleton<IConfigurationProfile>(profileService);

        var app = builder.Build();

        // Force the options to be resolved, which triggers PostConfigure
        var _ = app.Services.GetRequiredService<IdentityServerOptions>();

        profileInvoked.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void profile_should_not_be_invoked_when_not_configured()
    {
        var profileInvoked = false;

        var builder = WebApplication.CreateBuilder();

        var profileService = new TestProfileService("test-profile", options => profileInvoked = true);

        builder.Services.AddIdentityServer();
        builder.Services.AddSingleton<IConfigurationProfile>(profileService);

        var app = builder.Build();

        // Force the options to be resolved, which triggers PostConfigure
        var _ = app.Services.GetRequiredService<IdentityServerOptions>();

        profileInvoked.ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", Category)]
    public void multiple_profiles_should_all_be_invoked()
    {
        var profile1Invoked = false;
        var profile2Invoked = false;

        var builder = WebApplication.CreateBuilder();

        var profileService1 = new TestProfileService("test-profile-1", options => profile1Invoked = true);
        var profileService2 = new TestProfileService("test-profile-2", options => profile2Invoked = true);

        builder.Services.AddIdentityServer(options =>
        {
            options.ConfigurationProfiles.EnabledProfiles.Add("test-profile-1");
            options.ConfigurationProfiles.EnabledProfiles.Add("test-profile-2");
        });
        builder.Services.AddSingleton<IConfigurationProfile>(profileService1);
        builder.Services.AddSingleton<IConfigurationProfile>(profileService2);

        var app = builder.Build();

        // Force the options to be resolved, which triggers PostConfigure
        var _ = app.Services.GetRequiredService<IdentityServerOptions>();

        profile1Invoked.ShouldBeTrue();
        profile2Invoked.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public void profile_can_modify_options()
    {
        var builder = WebApplication.CreateBuilder();

        var profileService = new TestProfileService("test-profile", options =>
        {
            options.PushedAuthorization.Required = true;
        });

        builder.Services.AddIdentityServer(options =>
        {
            options.PushedAuthorization.Required = false;
            options.ConfigurationProfiles.EnabledProfiles.Add("test-profile");
        });
        builder.Services.AddSingleton<IConfigurationProfile>(profileService);

        var app = builder.Build();

        var options = app.Services.GetRequiredService<IdentityServerOptions>();
        options.PushedAuthorization.Required.ShouldBeTrue();
    }

    private class TestProfileService : IConfigurationProfile
    {
        private readonly Action<IdentityServerOptions> _applyAction;

        public TestProfileService(string profileName, Action<IdentityServerOptions> applyAction)
        {
            ProfileName = profileName;
            _applyAction = applyAction;
        }

        public string ProfileName { get; }

        public ProfileValidationResult ApplyProfile(IdentityServerOptions options)
        {
            _applyAction(options);
            return new ProfileValidationResult();
        }

        public ProfileValidationResult ValidateClient(IdentityServerOptions options, ClientConfigurationValidationContext context) =>
            // Not tested in these tests
            new ProfileValidationResult();
    }
}
