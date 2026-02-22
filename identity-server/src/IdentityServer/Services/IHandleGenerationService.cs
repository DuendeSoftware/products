// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Services;

/// <summary>
/// Interface for the handle generation service
/// </summary>
public interface IHandleGenerationService
{
    /// <summary>
    /// Generates a handle.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    Task<string> GenerateAsync(Ct ct, int length = 32);
}
