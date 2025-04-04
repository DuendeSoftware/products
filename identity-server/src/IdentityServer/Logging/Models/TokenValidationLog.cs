// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Logging.Models;

internal class TokenValidationLog
{
    // identity token
    public string ClientId { get; set; }
    public string ClientName { get; set; }
    public bool ValidateLifetime { get; set; }

    // access token
    public string AccessTokenType { get; set; }
    public string ExpectedScope { get; set; }
    public string TokenHandle { get; set; }
    public string JwtId { get; set; }

    // both
    public Dictionary<string, object> Claims { get; set; }

    public override string ToString() => LogSerializer.Serialize(this);
}
