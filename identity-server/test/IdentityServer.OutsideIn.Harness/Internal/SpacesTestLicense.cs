// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Duende.Spaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal;

/// <summary>
/// Registers a license that entitles every product SKU, Spaces included, so Spaces rows run the same features as rows
/// without Spaces. Uses reflection to construct the internal <c>Duende.Private.Licensing</c> types, like the helper in
/// the IdentityServer integration tests.
/// </summary>
internal static class SpacesTestLicense
{
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

    private static readonly string[] AllSkuIds = SkuIdsType
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(string))
        .Select(field => (string)field.GetValue(null)!)
        .ToArray();

    public static void RegisterEntitled(IServiceCollection services)
    {
        services.RemoveAll(LicenseValidatorType);
        services.AddSingleton(LicenseValidatorType, _ => CreateLicenseValidator(CreateV2License(AllSkuIds)));
    }

    private static object CreateV2License(IEnumerable<string> skuIds)
    {
        var listType = typeof(List<>).MakeGenericType(SkuEntitlementType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;

        foreach (var skuId in skuIds)
        {
            list.Add(Activator.CreateInstance(SkuEntitlementType,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                [skuId, (int?)null, (int?)null], null)!);
        }

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
