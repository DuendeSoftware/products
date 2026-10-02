// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.ApiScopes;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.IntegrationTests.Common;
using Duende.IdentityServer.IntegrationTests.TestFramework.TestIsolation;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Stores.Storage.Clients;
using Duende.Spaces;
using Duende.Storage;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement.TestIsolation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.IntegrationTests.TestFramework;

/// <summary>
/// Identifies how mTLS endpoints and Spaces are resolved for a
/// <see cref="SpacesMtlsIdentityServerFixture"/> instance.
/// </summary>
public enum SpacesMtlsRoutingMode
{
    /// <summary>
    /// Spaces are resolved by path (<c>/t/{space}</c>) and mTLS uses path-based routing
    /// (<c>MutualTls.DomainName</c> is empty).
    /// </summary>
    Path,

    /// <summary>
    /// Spaces are resolved by origin and mTLS uses subdomain-based routing
    /// (<c>MutualTls.DomainName = "mtls"</c>).
    /// </summary>
    Subdomain
}

/// <summary>
/// A configurable, single-scenario fixture combining a Duende Spaces-enabled IdentityServer with
/// simulated mTLS client certificate authentication. Construct a fresh instance per named test
/// fact (this is not an xUnit shared class fixture) and dispose it with <c>await using</c>.
/// </summary>
/// <remarks>
/// Owns both runtime-generated client certificates and the underlying <see cref="KestrelBasedTestServer"/>.
/// Exposes only scenario-oriented helpers; the Spaces/IdentityServer admin services and
/// <c>ISpaceContextAccessor</c> stay private so setup always goes through the fail-fast helpers below.
/// </remarks>
public sealed class SpacesMtlsIdentityServerFixture : IAsyncDisposable
{
    private readonly SpacesMtlsRoutingMode _routingMode;
    private readonly KestrelBasedTestServer _server;
    private readonly HashSet<string> _registeredHostAliases = new(StringComparer.OrdinalIgnoreCase);
    private bool _started;

    /// <summary>
    /// Runtime-generated client certificate for "space-a" scenarios. Distinct from
    /// <see cref="CertB"/>, generated fresh at construction time (no static/shared state).
    /// </summary>
    public X509Certificate2 CertA { get; }

    /// <summary>
    /// Runtime-generated client certificate for "space-b" scenarios. Distinct from
    /// <see cref="CertA"/>, generated fresh at construction time (no static/shared state).
    /// </summary>
    public X509Certificate2 CertB { get; }

    /// <summary>
    /// The canonical origin of the shared server hosting both spaces, e.g.
    /// <c>https://12-spaces-mtls.dev.localhost:5001</c>.
    /// </summary>
    public Uri CanonicalOrigin => _server.BaseAddress;

    public SpacesMtlsIdentityServerFixture(
        SpacesMtlsRoutingMode routingMode,
        ITestServerFixture webServerFixture,
        ITestOutputHelper output)
    {
        ArgumentNullException.ThrowIfNull(webServerFixture);
        ArgumentNullException.ThrowIfNull(output);

        _routingMode = routingMode;
        CertA = CreateEphemeralCertificate("CN=space-a-mtls-test");
        CertB = CreateEphemeralCertificate("CN=space-b-mtls-test");

        var dbName = $"spaces_mtls_{Guid.NewGuid():N}";

        _server = new KestrelBasedTestServer(
            "spaces-mtls",
            webServerFixture,
            output,
            services => ConfigureServices(services, dbName, routingMode),
            ConfigurePipeline);
    }

    /// <summary>
    /// Starts the server and migrates the shared database schema. Must be called before
    /// any of the scenario helpers below.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_started)
        {
            throw new InvalidOperationException("Fixture has already been initialized.");
        }

        await _server.StartAsync();
        _started = true;

        var schema = _server.GetRequiredService<IStorageInstanceSchema>();
        await schema.MigrateAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Creates a new space with the given name and match patterns, failing fast (with the
    /// operation, space name, and validation errors) if creation does not succeed.
    /// </summary>
    public async Task<SpaceId> CreateSpaceAsync(string name, params SpaceMatchPattern[] patterns)
    {
        EnsureStarted();

        using var scope = _server.Services.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<ISpaceAdmin>();

        var result = await admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = name,
                MatchPatterns = patterns
            },
            TestContext.Current.CancellationToken);

        EnsureSuccess(result, "CreateAsync(Space)", name);

        return result.Id!;
    }

    /// <summary>
    /// Creates an API scope inside the given space's context, failing fast if creation
    /// does not succeed.
    /// </summary>
    public async Task AddApiScopeToSpaceAsync(SpaceId spaceId, string scopeName)
    {
        EnsureStarted();

        using var scope = _server.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        var apiScopeAdmin = scope.ServiceProvider.GetRequiredService<IApiScopeAdmin>();

        using (accessor.SetSpace(spaceId))
        {
            var result = await apiScopeAdmin.CreateAsync(
                new CreateApiScope { Name = scopeName },
                TestContext.Current.CancellationToken);

            EnsureSuccess(result, "CreateAsync(ApiScope)", $"space {spaceId}, scope {scopeName}");
        }
    }

    /// <summary>
    /// Creates a client that authenticates via the given mTLS certificate's thumbprint,
    /// inside the given space's context, failing fast if creation does not succeed.
    /// </summary>
    /// <remarks>
    /// TEST-ONLY WORKAROUND for products-private#3584: <c>IClientAdmin</c> always hashes
    /// X509 secret values on write (<c>ClientAdmin.MapToSecretDso</c> / <c>CreateSecretAsync</c>),
    /// but the mTLS X509 secret validators compare the raw thumbprint. All non-secret client
    /// configuration still goes through the normal <see cref="IClientAdmin"/> path; only the
    /// stored secret value is corrected afterward, directly through the internal
    /// <see cref="ClientRepository"/> storage boundary (not by exposing raw services to tests
    /// or duplicating admin logic). Remove this post-write correction once #3584 is fixed and
    /// <see cref="IClientAdmin"/> persists X509 thumbprint secrets unhashed.
    /// </remarks>
    public async Task AddMtlsClientToSpaceAsync(SpaceId spaceId, string clientId, X509Certificate2 cert, string scopeName)
    {
        ArgumentNullException.ThrowIfNull(cert);
        EnsureStarted();

        using var scope = _server.Services.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ISpaceContextAccessor>();
        var clientAdmin = scope.ServiceProvider.GetRequiredService<IClientAdmin>();
        var clientRepository = scope.ServiceProvider.GetRequiredService<ClientRepository>();

        using (accessor.SetSpace(spaceId))
        {
            var result = await clientAdmin.CreateAsync(
                new CreateClient
                {
                    ClientId = clientId,
                    RequireClientSecret = true,
                    RequirePkce = false,
                    AllowedGrantTypes = [GrantType.ClientCredentials],
                    AllowedScopes = [scopeName],
                    ClientSecrets =
                    [
                        new CreateClientSecret
                        {
                            PlaintextValue = cert.Thumbprint,
                            Type = IdentityServerConstants.SecretTypes.X509CertificateThumbprint
                        }
                    ]
                },
                TestContext.Current.CancellationToken);

            EnsureSuccess(result, "CreateAsync(Client)", $"space {spaceId}, client {clientId}");

            await CorrectHashedX509ThumbprintAsync(clientRepository, result.Id!, cert.Thumbprint, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Rewrites the stored X509 thumbprint secret for the given client to <paramref name="rawThumbprint"/>,
    /// undoing the hashing <see cref="IClientAdmin"/> currently applies (products-private#3584). See
    /// <see cref="AddMtlsClientToSpaceAsync"/> for full context; this must run inside the same
    /// <c>SetSpace</c> context used to create the client.
    /// </summary>
    private static async Task CorrectHashedX509ThumbprintAsync(
        ClientRepository clientRepository, ClientId clientId, string rawThumbprint, CancellationToken ct)
    {
        var existing = await clientRepository.TryReadByIdAsync(clientId.Value, ct)
            ?? throw new InvalidOperationException($"Client {clientId} was not found immediately after creation.");

        var (dso, version) = existing;

        var correctedSecrets = dso.ClientSecrets
            .Select(s => s.Type == IdentityServerConstants.SecretTypes.X509CertificateThumbprint
                ? s with { Value = rawThumbprint }
                : s)
            .ToList();

        var updatedDso = dso with { ClientSecrets = correctedSecrets };

        var updateResult = await clientRepository.UpdateAsync(UuidV7.From(clientId.Value), updatedDso, version, ct);
        if (updateResult != UpdateResult.Success)
        {
            throw new InvalidOperationException($"Failed to correct X509 thumbprint secret for client {clientId}: {updateResult}.");
        }
    }

    /// <summary>
    /// Creates a client without a simulated client certificate, targeting the shared server.
    /// </summary>
    public HttpClient CreateClient()
    {
        EnsureStarted();
        return _server.CreateClient();
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> that presents <paramref name="cert"/> as a simulated
    /// client certificate via <see cref="MtlsMessageHandler"/>, targeting the shared server.
    /// </summary>
    public HttpClient CreateMtlsClient(X509Certificate2 cert)
    {
        ArgumentNullException.ThrowIfNull(cert);
        EnsureStarted();

#pragma warning disable CA2000 // Ownership transferred to HttpClient via disposeHandler: true
        var handler = new MtlsMessageHandler(_server.CreateHandler(), cert);
#pragma warning restore CA2000

#pragma warning disable CA5400 // CRL check intentionally disabled for test infrastructure
        return new HttpClient(handler, disposeHandler: true)
#pragma warning restore CA5400
        {
            BaseAddress = _server.BaseAddress
        };
    }

    /// <summary>
    /// Builds the canonical origin for a path-resolved space, e.g. <c>https://host:port/t/space-a</c>.
    /// The returned origin includes the Spaces path prefix (<c>/t</c>) and the given
    /// <paramref name="spaceSlug"/>. Only meaningful in <see cref="SpacesMtlsRoutingMode.Path"/>.
    /// </summary>
    public Uri GetSpacePathOrigin(string spaceSlug) => new(_server.BaseAddress, $"/t/{spaceSlug}");

    /// <summary>
    /// Builds the origin for an origin-resolved space in subdomain mTLS mode. Registers the
    /// necessary host alias (idempotently) so requests to this origin reach the shared server.
    /// </summary>
    /// <param name="spaceSlug">A short, unique slug identifying the space, e.g. <c>space-a</c>.</param>
    /// <param name="useMtlsPrefix">
    /// When <see langword="true"/>, builds the <c>mtls.</c>-prefixed origin used for mTLS
    /// endpoint requests. When <see langword="false"/>, builds the canonical origin used for
    /// discovery and general requests.
    /// </param>
    public Uri GetSpaceSubdomainOrigin(string spaceSlug, bool useMtlsPrefix)
    {
        EnsureStarted();

        var canonicalHost = $"{_server.TestId}-{spaceSlug}.dev.localhost";
        var host = useMtlsPrefix ? $"mtls.{canonicalHost}" : canonicalHost;

        EnsureHostAliasRegistered(canonicalHost);
        EnsureHostAliasRegistered($"mtls.{canonicalHost}");

        var builder = new UriBuilder(_server.BaseAddress) { Host = host };
        return builder.Uri;
    }

    private void EnsureHostAliasRegistered(string hostname)
    {
        if (_registeredHostAliases.Add(hostname))
        {
            _server.RegisterHostAlias(hostname);
        }
    }

    private void EnsureStarted()
    {
        if (!_started)
        {
            throw new InvalidOperationException("Fixture has not been initialized. Call InitializeAsync first.");
        }
    }

    private static void EnsureSuccess<TId>(SaveResult<TId> result, string operation, string context)
        where TId : notnull
    {
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"{operation} failed for {context}: {result}");
        }
    }

    private static void ConfigureServices(IServiceCollection services, string dbName, SpacesMtlsRoutingMode routingMode)
    {
        services.AddRouting();

        services.AddAuthentication(opts =>
        {
            opts.AddScheme("Certificate", scheme =>
            {
                scheme.DisplayName = "Certificate";
                scheme.HandlerType = typeof(MockCertificateAuthenticationHandler);
            });
        });

        services.AddIdentityServer(options =>
            {
                options.EmitStaticAudienceClaim = true;
                options.KeyManagement.Enabled = false;
                options.MutualTls.Enabled = true;
                options.MutualTls.DomainName = routingMode == SpacesMtlsRoutingMode.Subdomain ? "mtls" : "";
            })
            .AddStorage(storage =>
                storage.AddSqlite(opt =>
                    opt.ConnectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared"))
            .AddConfigurationStorage()
            .AddOperationalStorage()
            .AddInMemoryDataExtensionSchemas([])
            .AddMutualTlsSecretValidators()
            .AddDeveloperSigningCredential(persistKey: false);

        // AddSpaces() must be called after AddStorage() so its IPartitionedStorageFactory registration
        // (a plain AddTransient, not TryAdd) is resolved in place of IdentityServer's default.
        services.AddSpaces();
        services.Configure<SpacesOptions>(o => o.FallbackToDefault = false);

        SpacesTestLicense.RegisterEntitled(services);
    }

    private static void ConfigurePipeline(WebAppWrapper app)
    {
        // Pipeline order is locked: the test certificate injection middleware must run before
        // space resolution (so PathBase rewriting for path-based spaces happens before
        // IdentityServer sees the request) and before UseIdentityServer (so the
        // ITlsConnectionFeature is present when IdentityServer's mTLS middleware authenticates).
        app.UseMiddleware<MtlsTestMiddleware>();
        app.UseSpaceResolution();
        app.UseIdentityServer();
    }

    private static X509Certificate2 CreateEphemeralCertificate(string subjectName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));

        // Re-export/re-import so the certificate carries an exportable, cross-platform-usable
        // private key rather than an ephemeral one tied to the original RSA instance.
        return X509CertificateLoader.LoadPkcs12(certificate.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    public async ValueTask DisposeAsync()
    {
        CertA.Dispose();
        CertB.Dispose();
        await _server.DisposeAsync();
    }
}

/// <summary>
/// Registers a Spaces license entitlement for test purposes. Uses reflection to construct the
/// internal <c>Duende.Private.Licensing</c> types, mirroring the pattern used by
/// <c>Duende.Spaces.TestSpacesLicense</c> (internal to the Spaces test project) and
/// IdentityServer's own <c>TestLicense</c> helper.
/// </summary>
internal static class SpacesTestLicense
{
    // Force the Spaces assembly to load (which pulls in the licensing dependency).
    private static readonly Type SpacesMarkerType = typeof(ISpaceContextAccessor);

    private static readonly Assembly LicensingAssembly = SpacesMarkerType.Assembly
        .GetReferencedAssemblies()
        .Where(a => a.Name == "Duende.Private.Licensing")
        .Select(Assembly.Load)
        .First();

    private static readonly Type LicenseValidatorType =
        LicensingAssembly.GetType("Duende.Private.Licencing.V2.LicenseValidator")!;

    private static readonly Type V2LicenseType = LicensingAssembly.GetType("Duende.Private.Licencing.V2.V2License")!;

    private static readonly Type SkuEntitlementType =
        LicensingAssembly.GetType("Duende.Private.Licencing.V2.SkuEntitlement")!;

    private static readonly Type SkuIdsType = LicensingAssembly.GetType("Duende.Private.Licencing.V2.SkuIds")!;

    private static readonly string SpacesSkuId =
        (string)SkuIdsType.GetField("PLT_024", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

    /// <summary>
    /// Registers a <c>LicenseValidator</c> entitled to the Spaces SKU (<c>SkuIds.PLT_024</c>)
    /// directly into the container, replacing any existing registration.
    /// </summary>
    public static void RegisterEntitled(IServiceCollection services)
    {
        services.RemoveAll(LicenseValidatorType);
        services.AddSingleton(LicenseValidatorType, _ => CreateLicenseValidator(CreateV2License(SpacesSkuId)));
    }

    private static object CreateV2License(string skuId)
    {
        var listType = typeof(List<>).MakeGenericType(SkuEntitlementType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;

        var entitlement = Activator.CreateInstance(SkuEntitlementType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            [skuId, (int?)null, (int?)null], null)!;
        list.Add(entitlement);

        return Activator.CreateInstance(V2LicenseType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            [
                "P-003", "Test Company", "test@test.com", 1,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), list
            ], null)!;
    }

    private static object CreateLicenseValidator(object v2License)
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var logger = Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(LicenseValidatorType))!;

        return Activator.CreateInstance(LicenseValidatorType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            [v2License, logger, TimeProvider.System, configuration], null)!;
    }
}
