// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.External;

public sealed record ExternalAuthenticator(ExternalAuthenticatorName Name, ISubjectId SubjectId)
{
    internal static ExternalAuthenticator Load(ExternalAuthenticatorName name, ISubjectId subjectId) => new(name, subjectId);
}
