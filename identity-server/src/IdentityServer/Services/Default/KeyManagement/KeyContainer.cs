// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Services.KeyManagement;

/// <summary>
/// Container class for key.
/// </summary>
public abstract class KeyContainer
{
    /// <summary>
    /// Constructor for KeyContainer.
    /// </summary>
    public KeyContainer()
    {
    }

    /// <summary>
    /// Constructor for RsaKeyContainer.
    /// </summary>
    public KeyContainer(string id, string algorithm, DateTime created)
    {
        Id = id;
        Algorithm = algorithm;
        Created = created;
    }

    /// <summary>
    /// Key identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// The algorithm this key supports.
    /// </summary>
    public string Algorithm { get; set; }

    /// <summary>
    /// Indicates if key is contained in X509 certificate.
    /// </summary>
    public bool HasX509Certificate { get; set; }

    /// <summary>
    /// Date key was created.
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Creates AsymmetricSecurityKey.
    /// </summary>
    public abstract AsymmetricSecurityKey ToSecurityKey();
}
