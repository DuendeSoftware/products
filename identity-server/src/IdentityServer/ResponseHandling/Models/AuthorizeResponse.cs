// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Validation;

#pragma warning disable 1591

namespace Duende.IdentityServer.ResponseHandling;

public class AuthorizeResponse
{
    public ValidatedAuthorizeRequest Request { get; set; }
    public string RedirectUri => Request?.RedirectUri;
    public string State => Request?.State;
    public string Scope => Request?.ValidatedResources?.RawScopeValues.ToSpaceSeparatedString();

    public string IdentityToken { get; set; }
    public string AccessToken { get; set; }
    public int AccessTokenLifetime { get; set; }
    public string Code { get; set; }
    public string SessionState { get; set; }
    public string Issuer { get; set; }
    public Dictionary<string, string> CustomParameters { get; set; } = new();

    public string Error { get; set; }
    public string ErrorDescription { get; set; }
    public bool IsError => Error.IsPresent();
}
