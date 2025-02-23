// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

namespace Duende.IdentityServer.Stores.Serialization;

public class ClaimLite
{
    public string Type { get; set; }
    public string Value { get; set; }
    public string ValueType { get; set; }
    public string Issuer { get; set; }
}
