// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

namespace Duende.IdentityServer.EntityFramework.Entities;

public class SamlSigninState
{
    public long Id { get; set; }
    public Guid StateId { get; set; }
    public string SerializedState { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string ServiceProviderEntityId { get; set; }
}
