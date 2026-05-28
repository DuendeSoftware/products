// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// Creates concrete <see cref="IdentityProvider"/>-derived instances from a base
/// <see cref="IdentityProvider"/> model, using the <see cref="IdentityProvider.Type"/>
/// property to select the appropriate derived type.
/// </summary>
public interface IIdentityProviderFactory
{
    /// <summary>
    /// Creates a concrete <see cref="IdentityProvider"/>-derived instance from the given
    /// base model. Returns <c>null</c> if the type is not recognized.
    /// </summary>
    /// <param name="baseModel">The base identity provider model containing the type and properties.</param>
    /// <returns>A derived identity provider instance, or <c>null</c> if the type is unknown.</returns>
    IdentityProvider? Create(IdentityProvider baseModel);
}
