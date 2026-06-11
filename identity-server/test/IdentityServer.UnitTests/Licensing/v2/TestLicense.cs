// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Duende.IdentityServer.Licensing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace IdentityServer.UnitTests.Licensing.V2;

/// <summary>
/// Creates <see cref="IdentityServerLicenseValidator"/> instances backed by a
/// V2License with specific SKU entitlements. Uses reflection to construct the
/// internal types from the Duende.Private.Licensing package.
/// </summary>
internal static class TestLicense
{
    // Force the IdentityServer assembly to load (which pulls in the licensing dependency)
    private static readonly Type IsValidatorType = typeof(IdentityServerLicenseValidator);

    private static readonly Assembly LicensingAssembly = IsValidatorType.Assembly
        .GetReferencedAssemblies()
        .Where(a => a.Name == "Duende.Private.Licensing")
        .Select(Assembly.Load)
        .First();

    private static readonly Type LicenseValidatorType =
        LicensingAssembly.GetType("Duende.Private.Licencing.V2.LicenseValidator")!;

    private static readonly Type V2LicenseType = LicensingAssembly.GetType("Duende.Private.Licencing.V2.V2License")!;

    private static readonly Type SkuEntitlementType =
        LicensingAssembly.GetType("Duende.Private.Licencing.V2.SkuEntitlement")!;

    /// <summary>
    /// Creates an <see cref="IdentityServerLicenseValidator"/> whose license includes
    /// exactly the specified SKU entitlements (as boolean features with no limit/grace).
    /// </summary>
    internal static (IdentityServerLicenseValidator LicenseValidator, FakeLogCollector Logs) CreateValidator(
        params string[] entitledSkuIds)
    {
        var license = CreateV2License(entitledSkuIds);
        var (validator, logs) = CreateLicenseValidator(license);
        return (CreateIdentityServerLicenseValidator(validator), logs);
    }

    /// <summary>
    /// Creates an <see cref="IdentityServerLicenseValidator"/> with a quantized entitlement
    /// (one that has a numeric limit).
    /// </summary>
    internal static IdentityServerLicenseValidator CreateValidatorWithLimit(
        string skuId, int limit)
    {
        var license = CreateV2LicenseWithLimit(skuId, limit);
        var (validator, _) = CreateLicenseValidator(license);
        return CreateIdentityServerLicenseValidator(validator);
    }

    private static IdentityServerLicenseValidator CreateIdentityServerLicenseValidator(object licenseValidator)
    {
        var ctor = typeof(IdentityServerLicenseValidator)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            .First();
        return (IdentityServerLicenseValidator)ctor.Invoke([licenseValidator]);
    }

    internal static (IdentityServerLicenseValidator Validator, FakeLogCollector Logs) CreateValidatorWithoutLicense()
    {
        var license = Activator.CreateInstance(V2LicenseType, nonPublic: true)!;
        var (validator, logs) = CreateLicenseValidator(license);
        return (CreateIdentityServerLicenseValidator(validator), logs);
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
            [
                "P-003", "Test Company", "test@test.com", 1,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), list
            ], null)!;
    }

    private static object CreateV2LicenseWithLimit(string skuId, int limit)
    {
        var listType = typeof(List<>).MakeGenericType(SkuEntitlementType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;

        var entitlement = Activator.CreateInstance(SkuEntitlementType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            [skuId, (int?)limit, (int?)null], null)!;
        list.Add(entitlement);

        return Activator.CreateInstance(V2LicenseType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            [
                "P-003", "Test Company", "test@test.com", 1,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), list
            ], null)!;
    }

    private static (object Validator, FakeLogCollector Logs) CreateLicenseValidator(object v2License)
    {
        var configuration = new ConfigurationBuilder().Build();
        var loggerFactory = new NullLoggerFactory();
        var collector = FakeLogCollector.Create(new FakeLogCollectorOptions());
        var fakeLoggerType = typeof(FakeLogger<>).MakeGenericType(LicenseValidatorType);
        var logger = Activator.CreateInstance(fakeLoggerType, [collector])!;
        var validator = Activator.CreateInstance(LicenseValidatorType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            [v2License, logger, TimeProvider.System, configuration], null)!;
        return (validator, collector);
    }
}
