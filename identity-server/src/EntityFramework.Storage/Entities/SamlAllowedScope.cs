// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#pragma warning disable 1591

namespace Duende.IdentityServer.EntityFramework.Entities;

public class SamlAllowedScope
{
    public int Id { get; set; }
    public string Scope { get; set; }

    public int SamlServiceProviderId { get; set; }
    public SamlServiceProvider SamlServiceProvider { get; set; }
}
