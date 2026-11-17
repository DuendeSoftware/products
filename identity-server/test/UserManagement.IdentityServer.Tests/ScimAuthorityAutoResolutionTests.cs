// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Scim.Internal;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace IdentityServer8.UnitTests;

/// <summary>
/// Which of SCIM's two <c>JwtBearerOptions.ConfigurationManager</c> paths is in effect: an explicit
/// <c>Authority</c> keeps the framework's own discovery-based manager, and a blank <c>Authority</c>
/// gets IdentityServer's in-process, per-space manager (<see cref="ScimLocalIssuerConfigurationManager"/>).
/// The security-flag and connection-settings tests for the ordering fix itself live in
/// <see cref="ScimBearerOptionsTests"/>; this class only covers which manager ends up set.
/// </summary>
public sealed class ScimAuthorityAutoResolutionTests
{
    private const string Scheme = "DuendeScimBearer";

    [Fact]
    public void Explicit_authority_reaches_JwtBearerOptions_Authority()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddIdentityServer()
            .AddStorage(x => x.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddUserManagement(um =>
            {
                _ = um.EnableScim(_ => { });
                _ = um.ConfigureScimOAuth(o => o.Authority = "https://identity.example.com");
            });

        using var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(Scheme);

        options.Authority.ShouldBe("https://identity.example.com");
    }

    [Fact]
    public void No_authority_gets_the_local_issuer_configuration_manager()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddIdentityServer()
            .AddStorage(x => x.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddUserManagement(um =>
            {
                _ = um.EnableScim(_ => { });
            });

        using var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(Scheme);

        options.Authority.ShouldBeNull();
        _ = options.ConfigurationManager.ShouldBeOfType<ScimLocalIssuerConfigurationManager>();
        options.ConfigurationManager.ShouldNotBeAssignableTo<ConfigurationManager<OpenIdConnectConfiguration>>();
        options.ConfigurationManager.ShouldNotBeAssignableTo<StaticConfigurationManager<OpenIdConnectConfiguration>>();
    }

    [Fact]
    public void IssuerUri_set_and_no_Authority_leaves_ScimOAuthOptions_Authority_null_and_startup_succeeds()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.Configure<Duende.IdentityServer.Configuration.IdentityServerOptions>(
            o => o.IssuerUri = "https://identity.example.com");
        _ = services.AddIdentityServer()
            .AddStorage(x => x.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddUserManagement(um =>
            {
                _ = um.EnableScim(_ => { });
            });

        using var sp = services.BuildServiceProvider();

        // Startup succeeds (resolving the options doesn't throw), and IssuerUri is no longer
        // copied into ScimOAuthOptions.Authority: it stays unset, honored instead through
        // IIssuerNameService at request time.
        var scimOptions = sp.GetRequiredService<IOptions<Duende.UserManagement.Scim.ScimOAuthOptions>>().Value;
        scimOptions.Authority.ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task AddUserManagement_without_EnableScim_does_not_register_local_issuer_validation()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        _ = services.AddLogging();
        _ = services.AddAuthorization();
        _ = services.AddIdentityServer()
            .AddStorage(x => x.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddUserManagement(_ => { });

        using var sp = services.BuildServiceProvider();

        sp.GetService<ScimLocalIssuerMarker>().ShouldBeNull();
        sp.GetService<ScimLocalIssuerConfigurationManager>().ShouldBeNull();

        var schemeProvider = sp.GetRequiredService<IAuthenticationSchemeProvider>();
        (await schemeProvider.GetSchemeAsync(Scheme)).ShouldBeNull();

        var policyProvider = sp.GetRequiredService<IAuthorizationPolicyProvider>();
        (await policyProvider.GetPolicyAsync("DuendeScimRead")).ShouldBeNull();
    }

    [Fact]
    public async Task AddUserManagement_with_EnableScim_registers_scheme_and_policy()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        _ = services.AddLogging();
        _ = services.AddIdentityServer()
            .AddStorage(x => x.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddUserManagement(um => um.EnableScim(_ => { }));

        using var sp = services.BuildServiceProvider();

        var schemeProvider = sp.GetRequiredService<IAuthenticationSchemeProvider>();
        (await schemeProvider.GetSchemeAsync(Scheme)).ShouldNotBeNull();

        var policyProvider = sp.GetRequiredService<IAuthorizationPolicyProvider>();
        (await policyProvider.GetPolicyAsync("DuendeScimRead")).ShouldNotBeNull();
    }

    [Fact]
    public void EnableScim_called_twice_does_not_throw_or_duplicate_the_scheme()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        _ = services.AddLogging();
        _ = services.AddIdentityServer()
            .AddStorage(x => x.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddUserManagement(um =>
            {
                _ = um.EnableScim(_ => { });
                _ = um.EnableScim(_ => { });
            });

        using var sp = services.BuildServiceProvider();

        var schemeProvider = sp.GetRequiredService<IAuthenticationSchemeProvider>();
        var schemes = schemeProvider.GetAllSchemesAsync().GetAwaiter().GetResult()
            .Where(s => s.Name == Scheme)
            .ToList();
        schemes.Count.ShouldBe(1);
    }

    [Fact]
    public void AddUserManagement_with_EnableScim_registers_local_issuer_validation()
    {
        var services = new ServiceCollection();
        _ = services.AddLogging();
        _ = services.AddIdentityServer()
            .AddStorage(x => x.AddSqliteInMemory())
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddUserManagement(um =>
            {
                _ = um.EnableScim(_ => { });
            });

        using var sp = services.BuildServiceProvider();

        sp.GetService<ScimLocalIssuerMarker>().ShouldNotBeNull();
        sp.GetService<ScimLocalIssuerConfigurationManager>().ShouldNotBeNull();
    }
}
