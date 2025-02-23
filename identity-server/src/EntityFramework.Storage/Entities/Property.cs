// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

namespace Duende.IdentityServer.EntityFramework.Entities;

public abstract class Property
{
    public int Id { get; set; }
    public string Key { get; set; }
    public string Value { get; set; }
}