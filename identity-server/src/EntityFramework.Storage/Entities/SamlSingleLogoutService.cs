// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#pragma warning disable 1591

namespace Duende.IdentityServer.EntityFramework.Entities;

public class SamlSingleLogoutService
{
    public int Id { get; set; }
    public string Location { get; set; } = default!;
    public string Binding { get; set; } = default!;

    public int SamlServiceProviderId { get; set; }
    public SamlServiceProvider SamlServiceProvider { get; set; } = default!;
}
