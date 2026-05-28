// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#pragma warning disable 1591

namespace Duende.IdentityServer.EntityFramework.Entities;

public class SamlAssertionConsumerService
{
    public int Id { get; set; }
    public string Location { get; set; }
    public string Binding { get; set; }
    public int Index { get; set; }
    public bool IsDefault { get; set; }

    public int SamlServiceProviderId { get; set; }
    public SamlServiceProvider SamlServiceProvider { get; set; }
}
