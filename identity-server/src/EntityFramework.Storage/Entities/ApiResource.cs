// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

using System;
using System.Collections.Generic;

namespace Duende.IdentityServer.EntityFramework.Entities;

public class ApiResource
{
    public int Id { get; set; }
    public bool Enabled { get; set; } = true;
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string AllowedAccessTokenSigningAlgorithms { get; set; }
    public bool ShowInDiscoveryDocument { get; set; } = true;
    public bool RequireResourceIndicator { get; set; }
    public List<ApiResourceSecret> Secrets { get; set; }
    public List<ApiResourceScope> Scopes { get; set; }
    public List<ApiResourceClaim> UserClaims { get; set; }
    public List<ApiResourceProperty> Properties { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime? Updated { get; set; }
    public DateTime? LastAccessed { get; set; }
    public bool NonEditable { get; set; }
}
