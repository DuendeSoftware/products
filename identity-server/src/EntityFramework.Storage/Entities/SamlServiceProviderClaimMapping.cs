// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#pragma warning disable 1591

namespace Duende.IdentityServer.EntityFramework.Entities;

public class SamlServiceProviderClaimMapping
{
    public int Id { get; set; }
    /// <summary>
    /// The claim type (dictionary key).
    /// </summary>
    public string ClaimType { get; set; }
    /// <summary>
    /// The SAML attribute name (dictionary value).
    /// </summary>
    public string SamlAttributeName { get; set; }
    public int SamlServiceProviderId { get; set; }
    public SamlServiceProvider SamlServiceProvider { get; set; }
}
