// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Hosting.DynamicProviders;

/// <summary>
/// Default implementation of <see cref="IIdentityProviderFactory"/> that uses the
/// <see cref="DynamicProviderOptions"/> registrations to construct the appropriate
/// derived <see cref="IdentityProvider"/> type. Each derived type must have a constructor
/// that accepts a single <see cref="IdentityProvider"/> parameter (the copy constructor pattern).
/// </summary>
internal class DynamicIdentityProviderFactory : IIdentityProviderFactory
{
    private readonly DynamicProviderOptions _options;

    public DynamicIdentityProviderFactory(DynamicProviderOptions options) =>
        _options = options;

    public IdentityProvider? Create(IdentityProvider baseModel)
    {
        var providerType = _options.FindProviderType(baseModel.Type);
        if (providerType == null)
        {
            return null;
        }

        return providerType.CopyConstructor(baseModel);
    }
}
