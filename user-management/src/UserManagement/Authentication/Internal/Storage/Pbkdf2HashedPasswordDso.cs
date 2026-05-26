// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Internal.Storage;

internal static class Pbkdf2HashedPasswordDso
{
    internal sealed record V1(
        string Salt,
        string PseudorandomFunction,
        int IterationCount,
        int HashFunctionDigestSize,
        string MasterKey);
}
