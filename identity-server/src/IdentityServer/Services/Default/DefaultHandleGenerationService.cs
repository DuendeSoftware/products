// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Default handle generation service
/// </summary>
/// <seealso cref="IHandleGenerationService" />
public class DefaultHandleGenerationService : IHandleGenerationService
{
    /// <summary>
    /// Generates a handle.
    /// </summary>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public Task<string> GenerateAsync(int length) => Task.FromResult(CryptoRandom.CreateUniqueId(length, CryptoRandom.OutputFormat.Hex));
}
