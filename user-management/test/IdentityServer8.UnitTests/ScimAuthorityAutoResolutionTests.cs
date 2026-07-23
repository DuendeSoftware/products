// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Scim;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IdentityServer8.UnitTests;

#pragma warning disable duende_experimental

public sealed class ScimAuthorityAutoResolutionTests
{
    [Fact]
    public void ShouldResolveAuthorityFromIssuerUri()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.Configure<IdentityServerOptions>(o => o.IssuerUri = "https://identity.example.com");
        _ = services.AddIdentityServer()
            .AddUserManagement(um =>
            {
                _ = um.EnableScim(_ => { });
                _ = um.AddSqliteInMemoryStore();
            });

        using var sp = services.BuildServiceProvider();

        // Act — trigger post-configure by resolving the options
        var scimOptions = sp.GetRequiredService<IOptions<ScimOAuthOptions>>().Value;

        // Assert
        scimOptions.Authority.ShouldBe("https://identity.example.com");
    }

    [Fact]
    public void ShouldNotOverrideExplicitAuthority()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.Configure<IdentityServerOptions>(o => o.IssuerUri = "https://identity.example.com");
        _ = services.Configure<ScimOAuthOptions>(o => o.Authority = "https://custom.example.com");
        _ = services.AddIdentityServer()
            .AddUserManagement(um =>
            {
                _ = um.EnableScim(_ => { });
                _ = um.AddSqliteInMemoryStore();
            });

        using var sp = services.BuildServiceProvider();

        // Act
        var scimOptions = sp.GetRequiredService<IOptions<ScimOAuthOptions>>().Value;

        // Assert
        scimOptions.Authority.ShouldBe("https://custom.example.com");
    }

    [Fact]
    public void ShouldFailValidationWhenIssuerUriIsNullAndNoExplicitAuthority()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.Configure<IdentityServerOptions>(o => o.IssuerUri = null);
        _ = services.AddIdentityServer()
            .AddUserManagement(um =>
            {
                _ = um.EnableScim(_ => { });
                _ = um.AddSqliteInMemoryStore();
            });

        using var sp = services.BuildServiceProvider();

        // Act & Assert — Authority is null and no custom policy, so validation should fail
        var ex = Should.Throw<OptionsValidationException>(
            () => sp.GetRequiredService<IOptions<ScimOAuthOptions>>().Value);
        ex.Message.ShouldContain("Authority must be configured");
    }

    [Fact]
    public void ShouldFailValidationWhenIssuerUriIsEmptyAndNoExplicitAuthority()
    {
        // Arrange
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.Configure<IdentityServerOptions>(o => o.IssuerUri = "");
        _ = services.AddIdentityServer()
            .AddUserManagement(um =>
            {
                _ = um.EnableScim(_ => { });
                _ = um.AddSqliteInMemoryStore();
            });

        using var sp = services.BuildServiceProvider();

        // Act & Assert — Authority remains null (empty IssuerUri is not copied), validation should fail
        var ex = Should.Throw<OptionsValidationException>(
            () => sp.GetRequiredService<IOptions<ScimOAuthOptions>>().Value);
        ex.Message.ShouldContain("Authority must be configured");
    }
}
