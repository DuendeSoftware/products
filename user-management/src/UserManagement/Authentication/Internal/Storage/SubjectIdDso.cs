// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Internal.Storage;

internal static class SubjectIdDso
{
    internal sealed record V1(string Type, string Value)
    {
        internal const string TypeEmail = "Email";
        internal const string TypeOpaque = "Opaque";
        internal const string TypePhoneNumber = "PhoneNumber";
    }
}
