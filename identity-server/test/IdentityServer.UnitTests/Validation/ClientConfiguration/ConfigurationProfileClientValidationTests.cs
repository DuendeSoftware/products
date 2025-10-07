// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UnitTests.Common;

namespace IdentityServer.UnitTests.Validation.ClientConfiguration;

public class ConfigurationProfileClientValidationTests
{
    private const string Category = "Client Configuration - Profiles";
    public Client TestClient => new Client()
    {
        ClientId = "c",
        ClientSecrets = [new Secret("secret".Sha256())],
        AllowedGrantTypes = GrantTypes.ClientCredentials,
    };

    private class TestProfile : IConfigurationProfileService
    {
        public string ProfileName => "test_profile";
        public bool ValidateCalled { get; private set; }
        public bool CauseError { get; set; }
        public string? ErrorMessage { get; set; } = "profile error";

        public void ApplyProfile(IdentityServerOptions options) { /* no-op */ }
        public void ValidateClient(IdentityServerOptions options, ClientConfigurationValidationContext context)
        {
            ValidateCalled = true;
            if (CauseError)
            {
                context.SetError(ErrorMessage!);
            }
        }
    }

    private static (IClientConfigurationValidator validator, TestProfile profile, IdentityServerOptions options, MockLogger<DefaultClientConfigurationValidator> logger) CreateSystem(params string[] enabledProfiles)
    {
        var services = new ServiceCollection();
        var options = new IdentityServerOptions();
        foreach (var p in enabledProfiles)
        {
            options.ConfigurationProfile.Profiles.Add(p);
        }
        var profile = new TestProfile();
        var logger = new MockLogger<DefaultClientConfigurationValidator>();
        services.AddSingleton<IConfigurationProfileService>(profile);
        services.AddSingleton<ILogger<DefaultClientConfigurationValidator>>(logger);
        services.AddSingleton(options);
        services.AddTransient<IClientConfigurationValidator, DefaultClientConfigurationValidator>();
        var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IClientConfigurationValidator>();
        return (validator, profile, options, logger);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task enabled_profile_should_invoke_profile_client_validation()
    {
        var (validator, profile, _, _) = CreateSystem("test_profile");
        var ctx = new ClientConfigurationValidationContext(TestClient);

        await validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeTrue();
        profile.ValidateCalled.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task profile_error_should_set_context_invalid()
    {
        var (validator, profile, _, _) = CreateSystem("test_profile");
        profile.CauseError = true;
        profile.ErrorMessage = "boom";
        var ctx = new ClientConfigurationValidationContext(TestClient);

        await validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeFalse();
        ctx.ErrorMessage.ShouldBe("boom");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task profile_not_enabled_should_not_invoke_validation()
    {
        var (validator, profile, _, _) = CreateSystem(); // no profiles enabled
        var ctx = new ClientConfigurationValidationContext(TestClient);

        await validator.ValidateAsync(ctx);

        profile.ValidateCalled.ShouldBeFalse();
        ctx.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task enabled_profile_with_no_service_should_log_warning()
    {
        var (validator, _, _, logger) = CreateSystem("missing_profile");
        var ctx = new ClientConfigurationValidationContext(TestClient);

        await validator.ValidateAsync(ctx);

        ctx.IsValid.ShouldBeTrue();
        logger.LogLevels.ShouldContain(LogLevel.Warning);
        logger.LogMessages.ShouldContain(msg => msg.Contains("missing_profile") && msg.Contains("No IConfigurationProfileService found"));
    }
}
