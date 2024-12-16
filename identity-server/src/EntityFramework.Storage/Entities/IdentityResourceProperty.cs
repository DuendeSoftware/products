// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

namespace Duende.IdentityServer.EntityFramework.Entities;

public class IdentityResourceProperty : Property
{
    public int IdentityResourceId { get; set; }
    public IdentityResource IdentityResource { get; set; }
}
