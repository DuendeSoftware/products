// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Saml;

/// <summary>
/// A Saml2 Subject, see core 2.4.1.
/// </summary>
public class Subject
{
    /// <summary>
    /// NameId
    /// </summary>
    public NameId? NameId { get; set; }

    /// <summary>
    /// SubjectConfirmation
    /// </summary>
    public SubjectConfirmation? SubjectConfirmation { get; set; }
}
