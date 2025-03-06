// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

namespace Duende.IdentityServer.EntityFramework.Entities;

public class PushedAuthorizationRequest
{
    public long Id { get; set; }
    public string ReferenceValueHash { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string Parameters { get; set; }
}
