// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Logging.Models;

internal class BackchannelAuthenticationRequestValidationLog
{
    public string ClientId { get; set; }
    public string ClientName { get; set; }
    public string Scopes { get; set; }

    public IEnumerable<string> AuthenticationContextReferenceClasses { get; set; }
    public string Tenant { get; set; }
    public string IdP { get; set; }

    public Dictionary<string, string> Raw { get; set; }

    public BackchannelAuthenticationRequestValidationLog(ValidatedBackchannelAuthenticationRequest request, IEnumerable<string> sensitiveValuesFilter)
    {
        Raw = request.Raw.ToScrubbedDictionary(sensitiveValuesFilter.ToArray());

        if (request.Client != null)
        {
            ClientId = request.Client.ClientId;
            ClientName = request.Client.ClientName;
        }

        if (request.RequestedScopes != null)
        {
            Scopes = request.RequestedScopes.ToSpaceSeparatedString();
        }
    }

    public override string ToString() => LogSerializer.Serialize(this);
}
