// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Testing;

namespace Duende.UserManagement;

/// <summary>
/// Builds a licence for tests that need real licence enforcement. By default the licence entitles every
/// SKU without limits and is valid for one year from registration. Use the builder methods to remove
/// features or set quantity limits, then call <see cref="Register"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is not a licence key. No JWT is created, signed, or loaded. The helper builds the parsed licence
/// object directly and registers it together with a real licence validator. The licence file lookup,
/// signature validation, claim parsing, and V1 licence mapping performed by <c>V2LicenseAccessor</c> are
/// bypassed. Genuine licence keys are signed by Duende and validated against a public key embedded in
/// Duende.Private.Licensing, so tests cannot produce one.
/// </para>
/// <para>
/// Feature and quantity enforcement, grace thresholds, and licence log output use the production
/// validator, so tests exercise how User Management responds to its entitlements. They do not verify
/// that a genuine licence key is loaded or parsed correctly.
/// </para>
/// <para>
/// The licensing types are internal to Duende.Private.Licensing and not visible to test assemblies, so
/// they are constructed by reflection, mirroring the Spaces <c>TestSpacesLicense</c> helper.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var logs = TestUserManagementLicense.Create()
///     .WithUserLimit(5)
///     .Without(TestUserManagementLicense.Skus.RolesAndGroups)
///     .Register(services, timeProvider);
/// </code>
/// </example>
public sealed class TestUserManagementLicense
{
    /// <summary>
    /// The User Management SKU names, matching the constant names on the internal <c>SkuIds</c> type.
    /// </summary>
    public static class Skus
    {
        public const string UserCount = "UM_001";
        public const string Profiles = "UM_002";
        public const string RolesAndGroups = "UM_003";
        public const string Otp = "UM_004";
        public const string Totp = "UM_005";
        public const string Passkey = "UM_006";
        public const string RecoveryCode = "UM_007";
        public const string Password = "UM_008";
        public const string AccountLockout = "UM_009";
        public const string ExternalIdpLinking = "UM_010";
        public const string SelfService = "UM_011";
        public const string Administration = "UM_012";
        public const string Registration = "UM_013";
        public const string UserEvents = "UM_014";
        public const string AdvancedPasswordPolicies = "UM_015";
        public const string PerSpacePolicies = "UM_016";
        public const string InboundScim = "PLT_013";
    }

    private static readonly Assembly LicensingAssembly = typeof(UserSubjectId).Assembly
        .GetReferencedAssemblies()
        .Where(a => a.Name == "Duende.Private.Licensing")
        .Select(Assembly.Load)
        .First();

    private static readonly Type LicenseValidatorType =
        LicensingAssembly.GetType("Duende.Private.Licencing.V2.LicenseValidator", throwOnError: true)!;

    private static readonly Type V2LicenseType =
        LicensingAssembly.GetType("Duende.Private.Licencing.V2.V2License", throwOnError: true)!;

    private static readonly Type SkuEntitlementType =
        LicensingAssembly.GetType("Duende.Private.Licencing.V2.SkuEntitlement", throwOnError: true)!;

    private static readonly Type SkuIdsType =
        LicensingAssembly.GetType("Duende.Private.Licencing.V2.SkuIds", throwOnError: true)!;

    private static readonly MethodInfo CalculateGraceMethod =
        LicensingAssembly.GetType("Duende.Private.Licencing.V2.GraceCalculator", throwOnError: true)!
            .GetMethod("CalculateGrace", BindingFlags.Public | BindingFlags.Static)!;

    private readonly Dictionary<string, int?> _entitlements;

    private TestUserManagementLicense() =>
        _entitlements = SkuIdsType.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral || f.IsInitOnly)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(skuId => skuId, _ => (int?)null, StringComparer.Ordinal);

    /// <summary>
    /// Starts a licence that entitles every SKU without limits, valid for one year from registration.
    /// </summary>
    public static TestUserManagementLicense Create() => new();

    /// <summary>
    /// Limits the number of licensed users.
    /// </summary>
    public TestUserManagementLicense WithUserLimit(int limit) => WithLimit(Skus.UserCount, limit);

    /// <summary>
    /// Entitles <paramref name="sku"/> with a quantity limit. The grace threshold is calculated the same
    /// way as for issued licences.
    /// </summary>
    /// <param name="sku">A SKU name from <see cref="Skus"/>.</param>
    /// <param name="limit">The licensed quantity.</param>
    public TestUserManagementLicense WithLimit(string sku, int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        _entitlements[ResolveSkuId(sku)] = limit;
        return this;
    }

    /// <summary>
    /// Removes the entitlement for each of <paramref name="skus"/>, so the licence no longer includes those features.
    /// </summary>
    /// <param name="skus">SKU names from <see cref="Skus"/>.</param>
    public TestUserManagementLicense Without(params string[] skus)
    {
        ArgumentNullException.ThrowIfNull(skus);
        foreach (var sku in skus)
        {
            _ = _entitlements.Remove(ResolveSkuId(sku));
        }

        return this;
    }

    /// <summary>
    /// Registers the licence and a validator that uses it, replacing any existing licence registration.
    /// </summary>
    /// <returns>The collector that receives the licence validator's log output.</returns>
    public FakeLogCollector Register(IServiceCollection services, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var collector = FakeLogCollector.Create(new FakeLogCollectorOptions());
        var logger = Activator.CreateInstance(typeof(FakeLogger<>).MakeGenericType(LicenseValidatorType), [collector])!;

        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt.AddYears(1);
        var license = CreateV2License(issuedAt, expiresAt);
        var validator = Activator.CreateInstance(LicenseValidatorType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            [license, logger, timeProvider, new ConfigurationBuilder().Build()], null)!;

        _ = services.RemoveAll(V2LicenseType);
        _ = services.RemoveAll(LicenseValidatorType);
        _ = services.AddSingleton(V2LicenseType, license);
        _ = services.AddSingleton(LicenseValidatorType, validator);

        return collector;
    }

    private static string ResolveSkuId(string sku)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        var field = SkuIdsType.GetField(sku, BindingFlags.Public | BindingFlags.Static)
            ?? throw new ArgumentException($"'{sku}' is not a known SKU. Use a name from {nameof(Skus)}.", nameof(sku));

        return (string)field.GetValue(null)!;
    }

    private object CreateV2License(DateTimeOffset issuedAt, DateTimeOffset expiresAt)
    {
        var entitlements = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(SkuEntitlementType))!;

        foreach (var (skuId, limit) in _entitlements)
        {
            var grace = limit.HasValue ? (int?)CalculateGraceMethod.Invoke(null, [limit.Value])! : null;
            _ = entitlements.Add(Activator.CreateInstance(SkuEntitlementType,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
                [skuId, limit, grace], null)!);
        }

        return Activator.CreateInstance(V2LicenseType,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null,
            ["P-003", "Test Company", "test@test.com", 1, issuedAt, expiresAt, entitlements], null)!;
    }
}
