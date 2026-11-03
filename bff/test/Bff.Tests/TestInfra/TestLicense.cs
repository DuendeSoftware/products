// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Duende.Bff.Licensing;
using Microsoft.Extensions.Logging.Testing;

namespace Duende.Bff.Tests.TestInfra;

internal static class TestLicense
{
    private static readonly Assembly LicensingAssembly = typeof(BffLicenseValidator).Assembly
        .GetReferencedAssemblies()
        .Where(assembly => assembly.Name == "Duende.Private.Licensing")
        .Select(Assembly.Load)
        .First();

    private static readonly Type LicenseValidatorType =
        LicensingAssembly.GetType("Duende.Private.Licencing.V2.LicenseValidator")!;

    private static readonly Type V2LicenseType = LicensingAssembly.GetType("Duende.Private.Licencing.V2.V2License")!;

    private static readonly Type SkuEntitlementType =
        LicensingAssembly.GetType("Duende.Private.Licencing.V2.SkuEntitlement")!;

    internal static (BffLicenseValidator Validator, FakeLogCollector Logs) CreateValidator(
        DateTimeOffset expiration, TimeProvider timeProvider)
    {
        var entitlements = Activator.CreateInstance(typeof(List<>).MakeGenericType(SkuEntitlementType))!;
        var license = Activator.CreateInstance(V2LicenseType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            ["P-003", "Test Company", "test@test.com", 1, expiration.AddYears(-1), expiration, entitlements], null)!;
        return CreateValidator(license, timeProvider);
    }

    internal static (BffLicenseValidator Validator, FakeLogCollector Logs) CreateValidatorWithoutLicense()
    {
        var license = Activator.CreateInstance(V2LicenseType, nonPublic: true)!;
        return CreateValidator(license, new FakeTimeProvider());
    }

    private static (BffLicenseValidator Validator, FakeLogCollector Logs) CreateValidator(
        object license, TimeProvider timeProvider)
    {
        var logs = FakeLogCollector.Create(new FakeLogCollectorOptions());
        var logger = Activator.CreateInstance(typeof(FakeLogger<>).MakeGenericType(LicenseValidatorType), [logs])!;
        var licenseValidator = Activator.CreateInstance(LicenseValidatorType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            [license, logger, timeProvider, new ConfigurationBuilder().Build()], null)!;
        var validator = (BffLicenseValidator)Activator.CreateInstance(typeof(BffLicenseValidator),
            BindingFlags.Public | BindingFlags.Instance, null, [licenseValidator], null)!;
        return (validator, logs);
    }
}
