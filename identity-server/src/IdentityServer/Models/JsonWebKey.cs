// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

namespace Duende.IdentityServer.Models;

public class JsonWebKey
{
    public string kty { get; set; }
    public string use { get; set; }
    public string kid { get; set; }
    public string x5t { get; set; }
    public string e { get; set; }
    public string n { get; set; }
    public string[] x5c { get; set; }
    public string alg { get; set; }

    public string x { get; set; }
    public string y { get; set; }
    public string crv { get; set; }
}
