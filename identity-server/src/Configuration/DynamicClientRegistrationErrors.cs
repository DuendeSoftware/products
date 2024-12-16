// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Error codes defined by RFC 7591
/// </summary>
public static class DynamicClientRegistrationErrors
{
    /// <summary>
    /// The value of one or more redirection URIs is invalid.
    /// </summary>
    public const string InvalidRedirectUri = "invalid_redirect_uri";

    /// <summary>
    /// The value of one of the client metadata fields is invalid and the
    /// server has rejected this request.
    /// </summary>
    public const string InvalidClientMetadata = "invalid_client_metadata";

    /// <summary>
    /// The software statement presented is invalid.
    /// </summary>
    public const string InvalidSoftwareStatement = "invalid_software_statement";

    /// <summary>
    /// The software statement presented is not approved for use by this
    /// authorization server.
    /// </summary>
    public const string UnapprovedSoftwareStatement = "unapproved_software_statement";
}
