// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityServer.Models;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Interface for the key material service
/// </summary>
public interface IKeyMaterialService
{
    /// <summary>
    /// Gets all validation keys.
    /// </summary>
    Task<IEnumerable<SecurityKeyInfo>> GetValidationKeysAsync();

    /// <summary>
    /// Gets the signing credentials.
    /// </summary>
    /// <param name="allowedAlgorithms">Collection of algorithms used to filter the server supported algorithms. 
    /// A value of null or empty indicates that the server default should be returned.</param>
    Task<SigningCredentials> GetSigningCredentialsAsync(IEnumerable<string>? allowedAlgorithms = null);

    /// <summary>
    /// Gets all signing credentials.
    /// </summary>
    Task<IEnumerable<SigningCredentials>> GetAllSigningCredentialsAsync();
}
