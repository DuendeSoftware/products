// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services.KeyManagement;
using Duende.IdentityServer.Stores;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Services;

/// <summary>
/// The default key material service
/// </summary>
/// <seealso cref="IKeyMaterialService" />
public class DefaultKeyMaterialService : IKeyMaterialService
{
    private readonly IEnumerable<IValidationKeysStore> _validationKeysStores;
    private readonly IEnumerable<ISigningCredentialStore> _signingCredentialStores;
    private readonly IAutomaticKeyManagerKeyStore _keyManagerKeyStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultKeyMaterialService"/> class.
    /// </summary>
    /// <param name="validationKeysStores">The validation keys stores.</param>
    /// <param name="signingCredentialStores">The signing credential store.</param>
    /// <param name="keyManagerKeyStore">The store for automatic key management.</param>
    public DefaultKeyMaterialService(
        IEnumerable<IValidationKeysStore> validationKeysStores,
        IEnumerable<ISigningCredentialStore> signingCredentialStores,
        IAutomaticKeyManagerKeyStore keyManagerKeyStore)
    {
        _validationKeysStores = validationKeysStores;
        _signingCredentialStores = signingCredentialStores;
        _keyManagerKeyStore = keyManagerKeyStore;
    }

    /// <inheritdoc/>
    public async Task<SigningCredentials> GetSigningCredentialsAsync(IEnumerable<string> allowedAlgorithms, CT ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultKeyMaterialService.GetSigningCredentials");

        if (IEnumerableExtensions.IsNullOrEmpty(allowedAlgorithms))
        {
            var list = _signingCredentialStores.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                var key = await list[i].GetSigningCredentialsAsync(ct);
                if (key != null)
                {
                    return key;
                }
            }

            var automaticKey = await _keyManagerKeyStore.GetSigningCredentialsAsync(ct);
            if (automaticKey != null)
            {
                return automaticKey;
            }

            throw new InvalidOperationException($"No signing credential registered.");
        }

        var credential =
            (await GetAllSigningCredentialsAsync(ct)).FirstOrDefault(c => allowedAlgorithms.Contains(c.Algorithm));
        if (credential is null)
        {
            throw new InvalidOperationException(
                $"No signing credential for algorithms ({allowedAlgorithms.ToSpaceSeparatedString()}) registered.");
        }

        return credential;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<SigningCredentials>> GetAllSigningCredentialsAsync(CT ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultKeyMaterialService.GetAllSigningCredentials");

        var credentials = new List<SigningCredentials>();

        foreach (var store in _signingCredentialStores)
        {
            var signingKey = await store.GetSigningCredentialsAsync(ct);
            if (signingKey != null)
            {
                credentials.Add(signingKey);
            }
        }

        var automaticSigningKeys = await _keyManagerKeyStore.GetAllSigningCredentialsAsync(ct);
        if (automaticSigningKeys != null)
        {
            credentials.AddRange(automaticSigningKeys);
        }

        return credentials;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<SecurityKeyInfo>> GetValidationKeysAsync(CT ct)
    {
        using var activity = Tracing.ServiceActivitySource.StartActivity("DefaultKeyMaterialService.GetValidationKeys");

        var keys = new List<SecurityKeyInfo>();

        var automaticSigningKeys = await _keyManagerKeyStore.GetValidationKeysAsync(ct);
        if (automaticSigningKeys?.Any() == true)
        {
            keys.AddRange(automaticSigningKeys);
        }

        foreach (var store in _validationKeysStores)
        {
            var validationKeys = await store.GetValidationKeysAsync(ct);
            keys.AddRange(validationKeys);
        }

        return keys;
    }
}
