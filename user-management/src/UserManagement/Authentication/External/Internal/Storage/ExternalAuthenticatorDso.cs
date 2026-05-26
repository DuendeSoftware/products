// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Internal.Storage;

namespace Duende.UserManagement.Authentication.External.Internal.Storage;

internal static class ExternalAuthenticatorDso
{
    internal sealed record V1(string Name, SubjectIdDso.V1 SubjectId);
}
