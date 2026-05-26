// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Internal;

internal sealed record Pbkdf2Inputs
{
    private Pbkdf2Inputs(
        Pbkdf2Salt salt,
        Pbkdf2PseudorandomFunctionName pseudorandomFunctionName,
        Pbkdf2IterationCount iterationCount,
        Pbkdf2HashFunctionDigestSize hashFunctionDigestSize)
    {
        Salt = salt;
        IterationCount = iterationCount;
        PseudorandomFunctionName = pseudorandomFunctionName;
        HashFunctionDigestSize = hashFunctionDigestSize;
    }

    internal Pbkdf2Inputs()
    {
        Salt = Pbkdf2Salt.New();
        PseudorandomFunctionName = Pbkdf2PseudorandomFunctionName.Default;
        IterationCount = Pbkdf2IterationCount.For(PseudorandomFunctionName);
        HashFunctionDigestSize = Pbkdf2HashFunctionDigestSize.For(PseudorandomFunctionName);
    }

    internal Pbkdf2Salt Salt { get; }

    internal Pbkdf2PseudorandomFunctionName PseudorandomFunctionName { get; }

    internal Pbkdf2IterationCount IterationCount { get; }

    internal Pbkdf2HashFunctionDigestSize HashFunctionDigestSize { get; }

    internal static Pbkdf2Inputs Load(
        Pbkdf2Salt salt,
        Pbkdf2PseudorandomFunctionName pseudorandomFunctionName,
        Pbkdf2IterationCount iterationCount,
        Pbkdf2HashFunctionDigestSize hashFunctionDigestSize) =>
        new(salt, pseudorandomFunctionName, iterationCount, hashFunctionDigestSize);
}
