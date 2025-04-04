// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.ResponseHandling;

namespace Duende.IdentityServer.Logging.Models;

internal class AuthorizeResponseLog
{
    public string SubjectId { get; set; }
    public string ClientId { get; set; }
    public string RedirectUri { get; set; }
    public string State { get; set; }

    public string Scope { get; set; }
    public string Error { get; set; }
    public string ErrorDescription { get; set; }


    public AuthorizeResponseLog(AuthorizeResponse response)
    {
        ClientId = response.Request?.Client?.ClientId;
        SubjectId = response.Request?.Subject?.GetSubjectId();
        RedirectUri = response.RedirectUri;
        State = response.State;
        Scope = response.Scope;
        Error = response.Error;
        ErrorDescription = response.ErrorDescription;
    }

    public override string ToString() => LogSerializer.Serialize(this);
}
