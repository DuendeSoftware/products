// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Duende.Spaces.Internal;
using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace Duende.Spaces;

/// <summary>
/// The only location in the Spaces solution allowed to construct a <c>V2License</c>. Uses
/// reflection to build the internal Duende.Private.Licensing types, mirroring the pattern used by
/// IdentityServer's <c>TestLicense</c> helper. Production code (<c>SpacesLicenseValidator</c>) has
/// no test-only construction path, so tests must register a <c>V2License</c>/<c>LicenseValidator</c>
/// pair directly into the container.
/// </summary>
public static class TestSpacesLicense
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

    private static readonly Type GraceCalculatorType =
        LicensingAssembly.GetType("Duende.Private.Licencing.V2.GraceCalculator")!;

    private static readonly MethodInfo CalculateGraceMethod =
        GraceCalculatorType.GetMethod("CalculateGrace", BindingFlags.Public | BindingFlags.Static)!;

    private static readonly string SpacesSkuId =
        (string)SkuIdsType.GetField("PLT_024", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

    private static readonly string SpaceLimitSkuId =
        (string)SkuIdsType.GetField("PLT_023", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;

    /// <summary>
    /// Registers a <c>LicenseValidator</c> backed by a <c>V2License</c> entitled to the Spaces SKU
    /// (<c>SkuIds.PLT_024</c>). 
    /// </summary>
    public static void RegisterEntitled(IServiceCollection services) =>
        RegisterLicenseValidator(services, [SpacesSkuId]);

    /// <summary>
    /// Registers a <c>V2License</c> entitled to the Spaces SKU (<c>SkuIds.PLT_024</c>) directly as a
    /// singleton in the container (rather than pre-building a <c>LicenseValidator</c>)
    /// </summary>
    public static object RegisterEntitledAndReturn(IServiceCollection services)
    {
        var license = CreateV2License([(SpacesSkuId, (int?)null)]);
        services.RemoveAll(V2LicenseType);
        services.AddSingleton(V2LicenseType, license);
        return license;
    }

    /// <summary>
    /// Resolves the <c>V2License</c> singleton from the container. The type is internal to the
    /// licensing assembly (no <c>InternalsVisibleTo</c> to this test assembly), so callers cannot
    /// use the generic <c>GetRequiredService&lt;V2License&gt;()</c> overload directly.
    /// </summary>
    public static object ResolveV2License(IServiceProvider services) =>
        services.GetRequiredService(V2LicenseType);

    /// <summary>
    /// Registers a <c>LicenseValidator</c> backed by a <c>V2License</c> that does NOT include the
    /// Spaces SKU (<c>SkuIds.PLT_024</c>). 
    /// </summary>
    public static void RegisterUnentitled(IServiceCollection services) =>
        RegisterLicenseValidator(services, []);

    /// <summary>
    /// Registers a <c>LicenseValidator</c> backed by a <c>V2License</c> entitled to the Spaces SKU
    /// (<c>SkuIds.PLT_024</c>) with a quantized limit on <c>SkuIds.PLT_023</c> set to
    /// <paramref name="spaceLimit"/>. Replaces any existing registration. Returns the
    /// <see cref="FakeLogCollector"/> attached to the validator's logger via <paramref name="logs"/>
    /// so callers can assert on log output.
    /// </summary>
    public static void RegisterEntitledWithSpaceLimit(
        IServiceCollection services, int spaceLimit, out FakeLogCollector logs)
    {
        var collector = FakeLogCollector.Create(new FakeLogCollectorOptions());
        var fakeLoggerType = typeof(FakeLogger<>).MakeGenericType(LicenseValidatorType);
        var logger = Activator.CreateInstance(fakeLoggerType, [collector])!;

        var license = CreateV2License([(SpacesSkuId, (int?)null), (SpaceLimitSkuId, (int?)spaceLimit)]);
        var validator = CreateLicenseValidator(license, logger);

        services.RemoveAll(LicenseValidatorType);
        services.AddSingleton(LicenseValidatorType, validator);

        logs = collector;
    }

    private static void RegisterLicenseValidator(IServiceCollection services, string[] entitledSkuIds)
    {
        services.RemoveAll(LicenseValidatorType);
        services.AddSingleton(LicenseValidatorType, _ =>
        {
            var license = CreateV2License(entitledSkuIds.Select(skuId => (skuId, (int?)null)).ToArray());
            return CreateLicenseValidator(license);
        });
    }

    private static object CreateV2License(IReadOnlyList<(string SkuId, int? Limit)> skus)
    {
        var listType = typeof(List<>).MakeGenericType(SkuEntitlementType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;

        foreach (var sku in skus)
        {
            var grace = sku.Limit.HasValue
                ? (int?)CalculateGraceMethod.Invoke(null, [sku.Limit.Value])!
                : null;
            var entitlement = Activator.CreateInstance(SkuEntitlementType,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                [sku.SkuId, sku.Limit, grace], null)!;
            list.Add(entitlement);
        }

        return Activator.CreateInstance(V2LicenseType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            [
                "P-003", "Test Company", "test@test.com", 1,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), list
            ], null)!;
    }

    private static object CreateLicenseValidator(object v2License, object? logger = null)
    {
        var configuration = new ConfigurationBuilder().Build();
        if (logger is null)
        {
            var fakeLoggerType = typeof(NullLogger<>).MakeGenericType(LicenseValidatorType);
            logger = Activator.CreateInstance(fakeLoggerType)!;
        }

        return Activator.CreateInstance(LicenseValidatorType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            [v2License, logger, TimeProvider.System, configuration], null)!;
    }
}

/// <summary>
/// Explicit "happy path" assertions proving that an entitled license allows the core Spaces
/// operations to succeed without throwing. The failure path (unentitled license throwing) is
/// exercised alongside each production class's own tests; this class guards the opposite case.
/// </summary>
public sealed class EntitledLicenseTests : IAsyncLifetime
{
    private ServiceProvider _services = null!;
    private ISpaceContextAccessor _spaceContext = null!;
    private ISpaceAdmin _admin = null!;
    private static Ct Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddSpaces();
        TestSpacesLicense.RegisterEntitled(sc);
        sc.AddStorageInternal(b => b.AddSqliteInMemory());
        _services = sc.BuildServiceProvider();

        var storageInstanceSchema = await _services.GetRequiredService<IStorageInstanceSchemaFactory>().GetStorageInstanceSchema(StorageInstanceId.Default, Ct);
        await storageInstanceSchema.MigrateAsync(Ct);

        _spaceContext = _services.GetRequiredService<ISpaceContextAccessor>();
        _admin = _services.GetRequiredService<ISpaceAdmin>();
    }

    public async ValueTask DisposeAsync() => await _services.DisposeAsync();

    [Fact]
    public void set_space_with_non_default_space_succeeds_when_entitled()
    {
        var spaceId = SpaceId.New();

        var act = () =>
        {
            using var scope = _spaceContext.SetSpace(spaceId);
        };

        act.ShouldNotThrow();
    }

    [Fact]
    public async Task get_storage_succeeds_when_entitled()
    {
        using var scope = _spaceContext.SetSpace(SpaceId.Default);
        var partitionedStorageFactory = _services.GetRequiredService<IPartitionedStorageFactory>();

        var act = async () => await partitionedStorageFactory.GetPartitionedStorageAsync(DataCategoryName.Spaces, Ct);

        await act.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task create_space_succeeds_when_entitled()
    {
        var act = async () => await _admin.CreateAsync(
            new CreateSpaceConfiguration
            {
                Name = "Entitled Space",
                MatchPatterns = [new SpaceMatchPattern { Origin = "https://entitled.example.com" }]
            },
            Ct);

        await act.ShouldNotThrowAsync();
    }
}
