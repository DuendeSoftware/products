// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

namespace Duende.IdentityServer.EntityFramework.Entities;

public class SamlLogoutSessionRequestIndex
{
    public long Id { get; set; }
    public string RequestId { get; set; }
    public long SamlLogoutSessionId { get; set; }
    public SamlLogoutSession SamlLogoutSession { get; set; }
}
