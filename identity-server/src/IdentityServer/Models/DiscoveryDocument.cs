// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

namespace Duende.IdentityServer.Models;

public class DiscoveryDocument
{
    public string issuer { get; set; }
    public string jwks_uri { get; set; }
    public string authorization_endpoint { get; set; }
    public string token_endpoint { get; set; }
    public string userinfo_endpoint { get; set; }
    public string end_session_endpoint { get; set; }
    public string check_session_iframe { get; set; }
    public string revocation_endpoint { get; set; }
    public string introspection_endpoint { get; set; }
    public bool? frontchannel_logout_supported { get; set; }
    public bool? frontchannel_logout_session_supported { get; set; }
    public string[] scopes_supported { get; set; }
    public string[] claims_supported { get; set; }
    public string[] response_types_supported { get; set; }
    public string[] response_modes_supported { get; set; }
    public string[] grant_types_supported { get; set; }
    public string[] subject_types_supported { get; set; }
    public string[] id_token_signing_alg_values_supported { get; set; }
    public string[] token_endpoint_auth_methods_supported { get; set; }
    public string[] code_challenge_methods_supported { get; set; }
}
