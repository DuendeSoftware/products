// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Scim.Internal;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IdentityServer8.UnitTests;

/// <summary>
/// Options-level tests for the ordering fix in ScimOAuthModule: an explicit Authority must reach
/// JwtBearerOptions before ASP.NET Core's own JwtBearerPostConfigureOptions builds the discovery
/// ConfigurationManager, and SCIM's security flags must stay enforced even if a host Configure
/// call after AddUserManagement tries to turn them off.
/// </summary>
public sealed class ScimBearerOptionsTests
{
    private const string Scheme = "DuendeScimBearer";

    private static (ServiceCollection Services, IIdentityServerBuilder IdentityServer) BaseServices()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        var identityServer = services.AddIdentityServer()
            .AddStorage(x => x.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage();
        return (services, identityServer);
    }

    [Fact]
    public void Explicit_authority_reaches_JwtBearerOptions_and_builds_a_ConfigurationManager()
    {
        // Arrange
        var (services, identityServer) = BaseServices();
        _ = identityServer.AddUserManagement(um =>
        {
            _ = um.EnableScim(_ => { });
            _ = um.ConfigureScimOAuth(o => o.Authority = "https://identity.example.com");
        });

        using var sp = services.BuildServiceProvider();

        // Act
        var options = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(Scheme);

        // Assert
        options.Authority.ShouldBe("https://identity.example.com");
        options.ConfigurationManager.ShouldNotBeNull();
    }

    [Fact]
    public void Host_Configure_after_AddUserManagement_overrides_Authority()
    {
        // Arrange
        var (services, identityServer) = BaseServices();
        _ = identityServer.AddUserManagement(um =>
        {
            _ = um.EnableScim(_ => { });
            _ = um.ConfigureScimOAuth(o => o.Authority = "https://identity.example.com");
        });
        _ = services.Configure<JwtBearerOptions>(Scheme, o => o.Authority = "https://override.example.com");

        using var sp = services.BuildServiceProvider();

        // Act
        var options = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(Scheme);

        // Assert
        options.Authority.ShouldBe("https://override.example.com");
    }

    [Fact]
    public void Host_Configure_cannot_turn_off_the_enforced_security_flags()
    {
        // Arrange
        var (services, identityServer) = BaseServices();
        _ = identityServer.AddUserManagement(um =>
        {
            _ = um.EnableScim(_ => { });
            _ = um.ConfigureScimOAuth(o => o.Authority = "https://identity.example.com");
        });
        _ = services.Configure<JwtBearerOptions>(Scheme, o =>
        {
            o.TokenValidationParameters.ValidateIssuer = false;
            o.TokenValidationParameters.ValidateAudience = false;
            o.MapInboundClaims = true;
        });

        using var sp = services.BuildServiceProvider();

        // Act
        var options = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(Scheme);

        // Assert
        options.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
        options.TokenValidationParameters.ValidateAudience.ShouldBeTrue();
        options.TokenValidationParameters.ValidateLifetime.ShouldBeTrue();
        options.TokenValidationParameters.RequireExpirationTime.ShouldBeTrue();
        options.MapInboundClaims.ShouldBeFalse();
    }

    [Fact]
    public void No_authority_set_leaves_JwtBearerOptions_Authority_null()
    {
        // Arrange
        var (services, identityServer) = BaseServices();
        _ = identityServer.AddUserManagement(um =>
        {
            _ = um.EnableScim(_ => { });
        });

        using var sp = services.BuildServiceProvider();

        // Act
        var options = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(Scheme);

        // Assert
        options.Authority.ShouldBeNull();
    }

    [Fact]
    public void InProcess_validation_sets_ValidTypes_to_AccessTokenJwtType()
    {
        // Arrange
        var (services, identityServer) = BaseServices();
        _ = identityServer.AddUserManagement(um => um.EnableScim(_ => { }));

        using var sp = services.BuildServiceProvider();

        // Act
        var options = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(Scheme);

        // Assert
        _ = options.ConfigurationManager.ShouldBeOfType<ScimLocalIssuerConfigurationManager>();
        options.TokenValidationParameters.ValidTypes.ShouldBe(["at+jwt"]);
    }

    [Fact]
    public void Host_Configure_clearing_ValidTypes_does_not_stick()
    {
        // Arrange
        var (services, identityServer) = BaseServices();
        _ = identityServer.AddUserManagement(um => um.EnableScim(_ => { }));
        _ = services.Configure<JwtBearerOptions>(Scheme, o => o.TokenValidationParameters.ValidTypes = null);

        using var sp = services.BuildServiceProvider();

        // Act
        var options = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(Scheme);

        // Assert
        options.TokenValidationParameters.ValidTypes.ShouldBe(["at+jwt"]);
    }

    [Fact]
    public void Explicit_authority_does_not_set_ValidTypes()
    {
        // Arrange
        var (services, identityServer) = BaseServices();
        _ = identityServer.AddUserManagement(um =>
        {
            _ = um.EnableScim(_ => { });
            _ = um.ConfigureScimOAuth(o => o.Authority = "https://identity.example.com");
        });

        using var sp = services.BuildServiceProvider();

        // Act
        var options = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(Scheme);

        // Assert
        options.TokenValidationParameters.ValidTypes.ShouldBeNull();
    }
}
