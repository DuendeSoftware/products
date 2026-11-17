// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.UserManagement.Authentication.Internal.Storage;

internal static class Pbkdf2HashedRecoveryCodeDso
{
    internal sealed record V1(
        string Salt,
        string PseudorandomFunction,
        int IterationCount,
        int HashFunctionDigestSize,
        string MasterKey);
}
