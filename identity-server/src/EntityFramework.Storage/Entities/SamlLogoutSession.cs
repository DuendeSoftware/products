// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

namespace Duende.IdentityServer.EntityFramework.Entities;

public class SamlLogoutSession
{
    public long Id { get; set; }
    public string LogoutId { get; set; }
    public string SerializedSession { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public uint Version { get; set; }
    public ICollection<SamlLogoutSessionRequestIndex> RequestIndices { get; set; }
}
