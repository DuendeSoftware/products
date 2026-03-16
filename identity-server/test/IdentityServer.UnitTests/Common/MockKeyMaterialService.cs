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

    public Task<IReadOnlyCollection<SigningCredentials>> GetAllSigningCredentialsAsync(Ct _) => Task.FromResult<IReadOnlyCollection<SigningCredentials>>(SigningCredentials);

    public Task<SigningCredentials> GetSigningCredentialsAsync(IEnumerable<string> allowedAlgorithms, Ct _) => Task.FromResult(SigningCredentials.FirstOrDefault());

    public Task<IReadOnlyCollection<SecurityKeyInfo>> GetValidationKeysAsync(Ct _) => Task.FromResult<IReadOnlyCollection<SecurityKeyInfo>>(ValidationKeys);
}
