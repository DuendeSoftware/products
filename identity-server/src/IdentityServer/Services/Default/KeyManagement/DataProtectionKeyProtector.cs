// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.DataProtection;

namespace Duende.IdentityServer.Services.KeyManagement;

/// <summary>
/// Implementation of IKeyProtector based on ASP.NET Core's data protection feature.
/// </summary>
public class DataProtectionKeyProtector : ISigningKeyProtector
{
    private readonly KeyManagementOptions _options;
    private readonly IDataProtector _dataProtectionProvider;

    /// <summary>
    /// Constructor for DataProtectionKeyProtector.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="dataProtectionProvider"></param>
    public DataProtectionKeyProtector(KeyManagementOptions options, IDataProtectionProvider dataProtectionProvider)
    {
        _options = options;
        _dataProtectionProvider = dataProtectionProvider.CreateProtector(nameof(DataProtectionKeyProtector));
    }

    /// <inheritdoc/>
    public SerializedKey Protect(KeyContainer key)
    {
        var data = KeySerializer.Serialize(key);

        if (_options.DataProtectKeys)
        {
            data = _dataProtectionProvider.Protect(data);
        }

        return new SerializedKey
        {
            Version = 1,
            Created = DateTime.UtcNow,
            Id = key.Id,
            Algorithm = key.Algorithm,
            IsX509Certificate = key.HasX509Certificate,
            Data = data,
            DataProtected = _options.DataProtectKeys,
        };
    }

    /// <inheritdoc/>
    public KeyContainer Unprotect(SerializedKey key)
    {
        var data = key.DataProtected ?
            _dataProtectionProvider.Unprotect(key.Data) :
            key.Data;

        if (key.IsX509Certificate)
        {
            return KeySerializer.Deserialize<X509KeyContainer>(data);
        }

        if (key.Algorithm.StartsWith('R') || key.Algorithm.StartsWith('P'))
        {
            return KeySerializer.Deserialize<RsaKeyContainer>(data);
        }

        if (key.Algorithm.StartsWith('E'))
        {
            return KeySerializer.Deserialize<EcKeyContainer>(data);
        }

        throw new Exception($"Invalid Algorithm: {key.Algorithm} for kid: {key.Id}");
    }
}
