// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.IntegrationTests.TestFramework;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.IdentityServer.Licensing;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.Licensing;

public class LicensingFixture : IAsyncLifetime
{
    public KestrelBasedTestServer IdentityServer = null!;

    public KestrelBasedTestServer Client = null!;

    public List<Client> Clients = [];

    public Action<IServiceCollection> ConfigureServices { get; set; } = _ => { };
    public Action<IIdentityServerBuilder> ConfigureIdentityServer { get; set; } = _ => { };
    public Action<IdentityServerOptions> ConfigureIdentityServerOptions { get; set; } = _ => { };

    public List<string> Licenses { get; set; } = [];

    public LicensingFixture(WebServerFixture fixture)
    {
        IdentityServer = new KestrelBasedTestServer("identityserver", fixture, new PrefixedTestOutputHelper(TestContext.Current.TestOutputHelper, "identityserver"),
            services =>
            {
                ConfigureServices(services);

                var identityServerBuilder = services.AddIdentityServer(options =>
                {
                    options.UserInteraction.LoginUrl = "/account/login";
                    //options.KeyManagement.Enabled = false;
                    ConfigureIdentityServerOptions(options);
                });

                ConfigureIdentityServer(identityServerBuilder);
                identityServerBuilder.AddInMemoryClients(Clients)
                    .AddInMemoryIdentityResources([
                        new IdentityResources.OpenId(),
                        new IdentityResources.Profile()
                    ]);

                if (Licenses.Any())
                {
                    services.RemoveAll<IdentityServerLicenseValidator>();
                    services.AddSingleton(TestLicense.CreateValidator(Licenses.ToArray()));
                }

            },
            webapp =>
            {
                webapp.UseIdentityServer();

                webapp.MapGet("/account/login", async (HttpContext c, CancellationToken ct) =>
                {
                    await c.SignInAsync(new IdentityServerUser("sub"));
                });
            });

        Client = new KestrelBasedTestServer("client", fixture, new PrefixedTestOutputHelper(TestContext.Current.TestOutputHelper, "client"),
            services =>
            {
                services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddOpenIdConnect(options =>
                    {
                        options.Authority = IdentityServer.BaseAddress.ToString();
                        options.ClientId = "client";
                        options.ClientSecret = "secret";
                        options.ResponseType = "code";
                        options.ResponseMode = "query";
                        options.MapInboundClaims = false;
                        options.SaveTokens = true;
                        options.DisableTelemetry = true;
                    });
            },
            webapp =>
            {
                _ = webapp.MapGet("/", () => "Ok");
                _ = webapp.MapGet("/login", async (HttpContext c, Ct ct) =>
                {
                    await c.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties { RedirectUri = "/" });
                });
            });

        Clients.Add(new Client
        {
            ClientId = "client",
            ClientSecrets = { new Secret("secret".Sha256()) },
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RedirectUris = { Client.BuildUrl("/signin-oidc").ToString() },
            PostLogoutRedirectUris = { Client.BuildUrl("/signout-callback-oidc").ToString() },
            FrontChannelLogoutUri = Client.BuildUrl("/signout-oidc").ToString(),
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (IdentityServer != null)
        {
            await IdentityServer.DisposeAsync();
        }

        if (Client != null)
        {
            await Client.DisposeAsync();
        }
    }

    public async ValueTask InitializeAsync()
    {
        await IdentityServer.StartAsync();
        await Client.StartAsync();
    }
}


/// <summary>
/// Creates <see cref="IdentityServerLicenseValidator"/> instances backed by a
/// V2License with specific SKU entitlements. Uses reflection to construct the
/// internal types from the Duende.Private.Licensing package.
/// </summary>
internal static class TestLicense
{
    // Resolve the licensing assembly via IdentityServer's assembly references
    private static readonly Assembly IdentityServerAssembly = typeof(IdentityServerConstants).Assembly;

    private static readonly Assembly LicensingAssembly = IdentityServerAssembly
        .GetReferencedAssemblies()
        .Where(a => a.Name == "Duende.Private.Licensing")
        .Select(Assembly.Load)
        .First();

    private static readonly Type LicenseValidatorType = LicensingAssembly.GetType("Duende.Private.Licencing.V2.LicenseValidator")!;
    private static readonly Type V2LicenseType = LicensingAssembly.GetType("Duende.Private.Licencing.V2.V2License")!;
    private static readonly Type SkuEntitlementType = LicensingAssembly.GetType("Duende.Private.Licencing.V2.SkuEntitlement")!;
    private static readonly Type SkusType = LicensingAssembly.GetType("Duende.Private.Licencing.V2.Skus")!;
    private static readonly Type IsLicenseValidatorType = IdentityServerAssembly.GetType("Duende.IdentityServer.Licensing.IdentityServerLicenseValidator")!;

    /// <summary>
    /// Returns all known SKU IDs by reading the private <c>Skus.All</c> field via reflection.
    /// </summary>
    internal static string[] GetAllSkus()
    {
        var allField = SkusType.GetField("All", BindingFlags.NonPublic | BindingFlags.Static)!;
        var allValue = allField.GetValue(null)!; // IReadOnlyDictionary<string, Sku>
        var keysProperty = allValue.GetType().GetProperty("Keys")!;
        var keys = (IEnumerable<string>)keysProperty.GetValue(allValue)!;
        return keys.ToArray();
    }

    /// <summary>
    /// Creates an <see cref="IdentityServerLicenseValidator"/> whose license includes
    /// exactly the specified SKU entitlements (as boolean features with no limit/grace).
    /// </summary>
    internal static IdentityServerLicenseValidator CreateValidator(params string[] entitledSkuIds)
    {
        var license = CreateV2License(entitledSkuIds);
        var validator = CreateLicenseValidator(license);
        return CreateIdentityServerLicenseValidator(validator);
    }

    private static IdentityServerLicenseValidator CreateIdentityServerLicenseValidator(object licenseValidator)
    {
        var ctor = IsLicenseValidatorType
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            .First();
        return (IdentityServerLicenseValidator)ctor.Invoke([licenseValidator]);
    }

    private static object CreateV2License(string[] skuIds)
    {
        var listType = typeof(List<>).MakeGenericType(SkuEntitlementType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;

        foreach (var skuId in skuIds)
        {
            var entitlement = Activator.CreateInstance(SkuEntitlementType,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                [skuId, (int?)null, (int?)null], null)!;
            list.Add(entitlement);
        }

        return Activator.CreateInstance(V2LicenseType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            ["P-003", "Test Company", "test@test.com", 1,
             DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), list], null)!;
    }

    private static object CreateLicenseValidator(object v2License)
    {
        var configuration = new ConfigurationBuilder().Build();
        var loggerFactory = new NullLoggerFactory();
        var createLoggerMethod = typeof(Microsoft.Extensions.Logging.LoggerFactoryExtensions)
            .GetMethod(nameof(Microsoft.Extensions.Logging.LoggerFactoryExtensions.CreateLogger), [typeof(Microsoft.Extensions.Logging.ILoggerFactory)])!
            .MakeGenericMethod(LicenseValidatorType);
        var logger = createLoggerMethod.Invoke(null, [loggerFactory]);
        return Activator.CreateInstance(LicenseValidatorType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            [v2License, logger, TimeProvider.System, configuration], null)!;
    }
}
