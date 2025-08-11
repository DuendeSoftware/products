// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Cryptography;
using Duende.IdentityServer.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Services.KeyManagement;

/// <summary>
/// Container class for ECDsaSecurityKey.
/// </summary>
public class EcKeyContainer : KeyContainer
{
    /// <summary>
    /// Constructor for EcKeyContainer.
    /// </summary>
    public EcKeyContainer() : base()
    {
    }

    /// <summary>
    /// Constructor for EcKeyContainer.
    /// </summary>
    public EcKeyContainer(ECDsaSecurityKey key, string algorithm, DateTime created)
        : base(key.KeyId, algorithm, created)
    {
        var parameters = key.ECDsa.ExportParameters(includePrivateParameters: true);
        D = parameters.D;
        Q = parameters.Q;
    }

    /// <summary>
    /// Private key for EC key
    /// </summary>
    public byte[] D { get; set; }

    /// <summary>
    /// Public key for EC key
    /// </summary>
    public ECPoint Q { get; set; }

    /// <inheritdoc/>
    public override AsymmetricSecurityKey ToSecurityKey()
    {
        var curve = Algorithm switch
        {
            "ES256" => "P-256",
            "ES384" => "P-384",
            "ES512" => "P-521",
            _ => throw new Exception("Invalid SigningAlgorithm")
        };

        var parameters = new ECParameters
        {
            Curve = CryptoHelper.GetCurveFromCrvValue(curve),
            D = D,
            Q = Q,
        };

#pragma warning disable CA2000 // See: https://github.com/dotnet/runtime/issues/99605
        var key = new ECDsaSecurityKey(ECDsa.Create(parameters))
#pragma warning restore CA2000
        {
            KeyId = Id
        };
        return key;
    }
}
