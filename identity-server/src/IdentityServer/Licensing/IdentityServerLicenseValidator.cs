// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Collections.Concurrent;
using Duende.Private.Licencing.V2;
using Microsoft.Extensions.Logging.Abstractions;

namespace Duende.IdentityServer.Licensing;

/// <summary>
/// IdentityServer license validation. Delegates to the shared <see cref="LicenseValidator"/>
/// infrastructure for rate-limited logging and entitlement checks.
/// </summary>
internal sealed class IdentityServerLicenseValidator(LicenseValidator validator)
{
    private readonly ConcurrentDictionary<string, byte> _clients = new();
    private readonly ConcurrentDictionary<string, byte> _issuers = new();
    private readonly ConcurrentDictionary<string, byte> _samlServiceProviders = new();
    private readonly ConcurrentDictionary<string, byte> _samlIdps = new();

    internal void ValidateDPoP() => validator.ValidateFeature(SkuIds.PTC_006);

    internal void ValidateResourceIsolation() => validator.ValidateFeature(SkuIds.IS_001);

    internal void ValidateCiba() => validator.ValidateFeature(SkuIds.PTC_022);

    internal void ValidatePar() => validator.ValidateFeature(SkuIds.PTC_004);

    internal void ValidateDynamicProviders() => validator.ValidateFeature(SkuIds.PLT_005);

    internal void ValidateServerSideSessions() => validator.ValidateFeature(SkuIds.PLT_021);

    internal void ValidateKeyManagement() => validator.ValidateFeature(SkuIds.PLT_004);

    internal void ValidateSamlIdp() => validator.ValidateFeature(SkuIds.PTC_010);

    internal void ValidateSamlIdp(string entityId)
    {
        if (_samlIdps.ContainsKey(entityId) || !_samlIdps.TryAdd(entityId, 0))
        {
            return;
        }

        validator.ValidateQuantized(SkuIds.PTC_014, _samlIdps.Count);
    }

    internal void ValidateSamlServiceProvider() => validator.ValidateFeature(SkuIds.PTC_011);

    internal void ValidateSamlServiceProvider(string entityId)
    {
        if (_samlServiceProviders.ContainsKey(entityId) || !_samlServiceProviders.TryAdd(entityId, 0))
        {
            return;
        }

        validator.ValidateQuantized(SkuIds.PTC_013, _samlServiceProviders.Count);
    }

    internal void ValidateClient(string clientId)
    {
        if (_clients.ContainsKey(clientId) || !_clients.TryAdd(clientId, 0))
        {
            return;
        }

        validator.ValidateQuantized(SkuIds.PTC_009, _clients.Count);
    }

    internal void ValidateIssuer(string issuer)
    {
        if (_issuers.ContainsKey(issuer) || !_issuers.TryAdd(issuer, 0))
        {
            return;
        }

        validator.ValidateQuantized(SkuIds.PLT_020, _issuers.Count);
    }

    internal void ValidateLicense() => validator.ValidateLicenseExpiry();

    internal static IdentityServerLicenseValidator CreateForTests()
    {
        var v2License = new V2LicenseAccessor(static () => null, NullLogger<V2LicenseAccessor>.Instance).Current;
        return new IdentityServerLicenseValidator(
            new LicenseValidator(v2License, NullLogger<LicenseValidator>.Instance, TimeProvider.System));
    }
}
