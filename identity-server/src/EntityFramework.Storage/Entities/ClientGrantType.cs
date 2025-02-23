// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

namespace Duende.IdentityServer.EntityFramework.Entities;

public class ClientGrantType
{
    public int Id { get; set; }
    public string GrantType { get; set; }

    public int ClientId { get; set; }
    public Client Client { get; set; }
}