// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.IdentityModel.Tokens;

namespace UnitTests.Common;

internal class MockKeyMaterialService : IKeyMaterialService
{
    public List<SigningCredentials> SigningCredentials = new List<SigningCredentials>();
    public List<SecurityKeyInfo> ValidationKeys = new List<SecurityKeyInfo>();

    public Task<IEnumerable<SigningCredentials>> GetAllSigningCredentialsAsync()
    {
        return Task.FromResult(SigningCredentials.AsEnumerable());
    }

    public Task<SigningCredentials> GetSigningCredentialsAsync(IEnumerable<string> allowedAlgorithms = null)
    {
        return Task.FromResult(SigningCredentials.FirstOrDefault());
    }

    public Task<IEnumerable<SecurityKeyInfo>> GetValidationKeysAsync()
    {
        return Task.FromResult(ValidationKeys.AsEnumerable());
    }
}
