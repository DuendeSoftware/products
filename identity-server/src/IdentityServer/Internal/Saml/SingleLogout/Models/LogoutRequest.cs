// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Internal.Saml.Infrastructure;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Duende.IdentityServer.Saml.Models;

namespace Duende.IdentityServer.Internal.Saml.SingleLogout.Models;

/// <summary>
/// Represents a SAML 2.0 LogoutRequest message.
/// </summary>
internal record LogoutRequest : ISamlRequest
{
    public static string MessageName => "SAML logout request";

    /// <summary>
    /// Gets or sets the unique identifier for this request.
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the SAML version. Must be "2.0".
    /// </summary>
    public string Version { get; set; } = SamlVersions.V2;

    /// <summary>
    /// Gets or sets the time instant of issue in UTC.
    /// </summary>
    public required DateTime IssueInstant { get; set; }

    /// <summary>
    /// Gets or sets the URI of the destination endpoint where this request is sent.
    /// </summary>
    public Uri? Destination { get; set; }

    /// <summary>
    /// Gets or sets the entity identifier of the issuer (sender) of this request.
    /// </summary>
    public required string Issuer { get; set; }

    /// <summary>
    /// Gets or sets the NameID identifying the principal that is being logged out.
    /// </summary>
    public required NameIdentifier NameId { get; set; }

    /// <summary>
    /// Gets or sets the SessionIndex identifying the session to be terminated.
    /// </summary>
    public required string SessionIndex { get; set; }

    /// <summary>
    /// Gets or sets the reason for the logout (optional).
    /// </summary>
    public LogoutReason? Reason { get; set; }

    /// <summary>
    /// Gets or sets the NotOnOrAfter time limit for the logout operation.
    /// </summary>
    public DateTime? NotOnOrAfter { get; set; }

    internal static class AttributeNames
    {
        public const string Id = "ID";
        public const string Version = "Version";
        public const string IssueInstant = "IssueInstant";
        public const string Reason = "Reason";
        public const string NotOnOrAfter = "NotOnOrAfter";
        public const string Destination = "Destination";
    }

    internal static class ElementNames
    {
        public const string RootElement = "LogoutRequest";
        public const string Issuer = "Issuer";
        public const string NameID = "NameID";
        public const string SessionIndex = "SessionIndex";
    }
}

/// <summary>
/// Represents the reason for logout in a LogoutRequest.
/// </summary>
internal enum LogoutReason
{
    /// <summary>
    /// User initiated the logout.
    /// </summary>
    User,

    /// <summary>
    /// Administrator initiated the logout.
    /// </summary>
    Admin,

    /// <summary>
    /// Logout due to global timeout.
    /// </summary>
    GlobalTimeout
}
