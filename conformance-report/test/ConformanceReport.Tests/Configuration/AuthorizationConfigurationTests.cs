// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.ConformanceReport.Configuration;

public class AuthorizationConfigurationTests
{
    [Fact]
    public void default_configuration_requires_authenticated_user()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddAuthorization();
        _ = services.AddConformanceReport();
        var provider = services.BuildServiceProvider();

        var authOptions = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        var conformanceOptions = provider.GetRequiredService<IOptions<ConformanceReportOptions>>().Value;

        // Act
        var policy = authOptions.GetPolicy(conformanceOptions.AuthorizationPolicyName);

        // Assert
        _ = policy.ShouldNotBeNull();
        policy.Requirements.ShouldContain(r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public void custom_authorization_is_applied()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddAuthorization();
        _ = services.AddConformanceReport(options =>
        {
            options.ConfigureAuthorization = policy =>
                policy.RequireRole("TestRole");
        });
        var provider = services.BuildServiceProvider();

        var authOptions = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        var conformanceOptions = provider.GetRequiredService<IOptions<ConformanceReportOptions>>().Value;

        // Act
        var policy = authOptions.GetPolicy(conformanceOptions.AuthorizationPolicyName);

        // Assert
        _ = policy.ShouldNotBeNull();
        var roleRequirement = policy.Requirements.OfType<RolesAuthorizationRequirement>().SingleOrDefault();
        _ = roleRequirement.ShouldNotBeNull();
        roleRequirement.AllowedRoles.ShouldContain("TestRole");
    }

    [Fact]
    public void null_configuration_does_not_register_policy()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddAuthorization();
        _ = services.AddConformanceReport(options =>
        {
            options.ConfigureAuthorization = null;
        });
        var provider = services.BuildServiceProvider();

        var authOptions = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        var conformanceOptions = provider.GetRequiredService<IOptions<ConformanceReportOptions>>().Value;

        // Act
        var policy = authOptions.GetPolicy(conformanceOptions.AuthorizationPolicyName);

        // Assert
        policy.ShouldBeNull();
    }

    [Fact]
    public void multiple_requirements_can_be_configured()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddAuthorization();
        _ = services.AddConformanceReport(options =>
        {
            options.ConfigureAuthorization = policy =>
            {
                _ = policy.RequireRole("Admin");
                _ = policy.RequireClaim("department", "IT");
            };
        });
        var provider = services.BuildServiceProvider();

        var authOptions = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        var conformanceOptions = provider.GetRequiredService<IOptions<ConformanceReportOptions>>().Value;

        // Act
        var policy = authOptions.GetPolicy(conformanceOptions.AuthorizationPolicyName);

        // Assert
        _ = policy.ShouldNotBeNull();
        var roleRequirement = policy.Requirements.OfType<RolesAuthorizationRequirement>().SingleOrDefault();
        _ = roleRequirement.ShouldNotBeNull();
        roleRequirement.AllowedRoles.ShouldContain("Admin");

        var claimRequirement = policy.Requirements.OfType<ClaimsAuthorizationRequirement>().SingleOrDefault();
        _ = claimRequirement.ShouldNotBeNull();
        claimRequirement.ClaimType.ShouldBe("department");
        _ = claimRequirement.AllowedValues.ShouldNotBeNull();
        claimRequirement.AllowedValues.ShouldContain("IT");
    }

    [Fact]
    public void empty_configuration_allows_anonymous()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddAuthorization();
        _ = services.AddConformanceReport(options =>
        {
            // Allow anonymous with assertion that always passes
            options.ConfigureAuthorization = policy => policy.RequireAssertion(_ => true);
        });
        var provider = services.BuildServiceProvider();

        var authOptions = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;
        var conformanceOptions = provider.GetRequiredService<IOptions<ConformanceReportOptions>>().Value;

        // Act
        var policy = authOptions.GetPolicy(conformanceOptions.AuthorizationPolicyName);

        // Assert
        _ = policy.ShouldNotBeNull();
        var assertionRequirement = policy.Requirements.OfType<AssertionRequirement>().SingleOrDefault();
        _ = assertionRequirement.ShouldNotBeNull();
    }
}
