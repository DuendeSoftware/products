// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.External;

/// <summary>
/// Represents an external authenticator (e.g., a social login provider) registered for a user,
/// combining the provider name with the user's subject identifier at that provider.
/// </summary>
/// <param name="Name">The name of the external authentication provider.</param>
/// <param name="SubjectId">The user's subject identifier at the external provider.</param>
public sealed record ExternalAuthenticator(ExternalAuthenticatorName Name, ISubjectId SubjectId)
{
    internal static ExternalAuthenticator Load(ExternalAuthenticatorName name, ISubjectId subjectId) => new(name, subjectId);
}
