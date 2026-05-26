// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Internal.Storage;

internal static class HashedPasswordDso
{
    internal sealed record V1(
        string AlgorithmId,
        string Hash,
        string Salt,
        Dictionary<string, string>? Parameters);
}
