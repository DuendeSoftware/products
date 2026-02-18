// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

internal static class SamlBindingExtensions
{
    internal static SamlBinding? FromUrnOrDefault(string? urn)
    {
        if (urn == null)
        {
            return null;
        }

        return FromUrn(urn);
    }

    internal static SamlBinding FromUrn(string urn) => urn switch
    {
        SamlConstants.Bindings.HttpRedirect => SamlBinding.HttpRedirect,
        SamlConstants.Bindings.HttpPost => SamlBinding.HttpPost,
        _ => throw new ArgumentOutOfRangeException(nameof(urn), urn, "Unknown SAML binding")
    };

    internal static string ToUrn(this SamlBinding binding) => binding switch
    {
        SamlBinding.HttpRedirect => SamlConstants.Bindings.HttpRedirect,
        SamlBinding.HttpPost => SamlConstants.Bindings.HttpPost,
        _ => throw new ArgumentOutOfRangeException(nameof(binding), binding, "Unknown SAML binding")
    };
}
