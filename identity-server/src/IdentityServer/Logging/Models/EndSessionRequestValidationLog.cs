// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Logging.Models;

internal class EndSessionRequestValidationLog
{
    public string ClientId { get; set; }
    public string ClientName { get; set; }
    public string SubjectId { get; set; }

    public string PostLogOutUri { get; set; }
    public string State { get; set; }

    public Dictionary<string, string> Raw { get; set; }

    public EndSessionRequestValidationLog(ValidatedEndSessionRequest request)
    {
        Raw = request.Raw.ToScrubbedDictionary(OidcConstants.EndSessionRequest.IdTokenHint);

        SubjectId = "unknown";

        var subjectClaim = request.Subject?.FindFirst(JwtClaimTypes.Subject);
        if (subjectClaim != null)
        {
            SubjectId = subjectClaim.Value;
        }

        if (request.Client != null)
        {
            ClientId = request.Client.ClientId;
            ClientName = request.Client.ClientName;
        }

        PostLogOutUri = request.PostLogOutUri;
        State = request.State;
    }

    public override string ToString() => LogSerializer.Serialize(this);
}
