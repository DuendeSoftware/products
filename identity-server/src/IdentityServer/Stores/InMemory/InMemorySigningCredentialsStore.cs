// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Stores;

/// <summary>
/// Default signing credentials store
/// </summary>
/// <seealso cref="ISigningCredentialStore" />
public class InMemorySigningCredentialsStore : ISigningCredentialStore
{
    private readonly SigningCredentials _credential;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemorySigningCredentialsStore"/> class.
    /// </summary>
    /// <param name="credential">The credential.</param>
    public InMemorySigningCredentialsStore(SigningCredentials credential) => _credential = credential;

    /// <summary>
    /// Gets the signing credentials.
    /// </summary>
    /// <returns></returns>
    public Task<SigningCredentials> GetSigningCredentialsAsync(CT ct)
    {
        using var activity = Tracing.StoreActivitySource.StartActivity("InMemorySigningCredentialsStore.GetSigningCredentials");

        return Task.FromResult(_credential);
    }
}
