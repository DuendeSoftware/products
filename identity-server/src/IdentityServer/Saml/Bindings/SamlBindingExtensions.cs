// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Saml.Bindings;

internal static class SamlBindingExtensions
{
    extension(SamlBinding binding)
    {
        internal string ToUrn() => binding switch
        {
            SamlBinding.HttpRedirect => SamlConstants.Bindings.HttpRedirect,
            SamlBinding.HttpPost => SamlConstants.Bindings.HttpPost,
            _ => throw new ArgumentOutOfRangeException(nameof(binding), binding, "Unknown SAML binding")
        };
    }
}
