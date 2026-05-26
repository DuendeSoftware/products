// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.Passwords;

namespace Duende.UserManagement.Authentication.Internal;

internal sealed record HashedPassword
{
    private HashedPassword(HashedPasswordData data) => Data = data;

    internal HashedPasswordData Data { get; }

    internal string AlgorithmId => Data.AlgorithmId;

    internal static HashedPassword From(string password, IPasswordHashAlgorithm algorithm) =>
        new(algorithm.Hash(password));

    internal static HashedPassword Load(HashedPasswordData data) => new(data);
}
