// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

namespace Duende.IdentityServer.EntityFramework.Entities;

public abstract class Secret
{
    public int Id { get; set; }
    public string Description { get; set; }
    public string Value { get; set; }
    public DateTime? Expiration { get; set; }
    public string Type { get; set; } = "SharedSecret";
    public DateTime Created { get; set; } = DateTime.UtcNow;
}