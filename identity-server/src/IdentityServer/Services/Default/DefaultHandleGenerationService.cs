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
    /// <inheritdoc/>
    public Task<string> GenerateAsync(Ct _, int length = 32) => Task.FromResult(CryptoRandom.CreateUniqueId(length, CryptoRandom.OutputFormat.Hex));
}
