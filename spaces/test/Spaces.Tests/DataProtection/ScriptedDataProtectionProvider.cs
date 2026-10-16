// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Duende.Spaces.DataProtection;

// Not thread-safe: only for single-threaded tests.
internal sealed class ScriptedDataProtectionProvider : IDataProtectionProvider
{
    public List<ScriptedProtector> Created { get; } = [];

    public Func<string[], bool> ThrowInvalidOperation { get; set; } = _ => false;

    public Func<string[], bool> Succeeds { get; set; } = _ => false;

    public Func<string[], bool> RequiresMigration { get; set; } = _ => false;

    public Func<string[], bool> WasRevoked { get; set; } = _ => false;

    public IDataProtector CreateProtector(string purpose) => Make([purpose]);

    internal ScriptedProtector Make(string[] chain)
    {
        var p = new ScriptedProtector(this, chain);
        Created.Add(p);
        return p;
    }

    public ScriptedProtector Unpartitioned(params string[] chain) =>
        Created.Last(p => p.Chain.SequenceEqual(chain));

    public ScriptedProtector Partitioned() => Created.Last(p => p.Chain[0] == "space" && p.Chain.Length > 2);

    internal sealed class ScriptedProtector(ScriptedDataProtectionProvider root, string[] chain) : IDataProtector, IPersistedDataProtector
    {
        public string[] Chain { get; } = chain;

        public int UnprotectCalls { get; private set; }

        public int DangerousCalls { get; private set; }

        public CryptographicException? Thrown { get; private set; }

        public IDataProtector CreateProtector(string purpose) => root.Make([.. Chain, purpose]);

        public byte[] Protect(byte[] plaintext) => plaintext;

        public byte[] Unprotect(byte[] protectedData)
        {
            UnprotectCalls++;
            return Fail();
        }

        public byte[] DangerousUnprotect(byte[] protectedData, bool ignoreRevocationErrors, out bool requiresMigration, out bool wasRevoked)
        {
            DangerousCalls++;
            requiresMigration = root.RequiresMigration(Chain);
            wasRevoked = root.WasRevoked(Chain);
            return Fail();
        }

        private byte[] Fail()
        {
            if (root.ThrowInvalidOperation(Chain))
            {
                throw new InvalidOperationException("inner failure");
            }

            if (root.Succeeds(Chain))
            {
                return [9];
            }

            Thrown = new CryptographicException(string.Join("/", Chain));
            throw Thrown;
        }
    }
}
