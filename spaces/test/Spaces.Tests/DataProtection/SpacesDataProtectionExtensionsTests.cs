// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.DataProtection;

namespace Duende.Spaces.DataProtection;

public class SpacesDataProtectionExtensionsTests
{
    [Fact]
    public void get_unpartitioned_on_plain_provider_returns_the_receiver()
    {
        var provider = new EphemeralDataProtectionProvider();

        provider.GetUnpartitioned().ShouldBeSameAs(provider);
    }

    [Fact]
    public void get_unpartitioned_on_plain_protector_returns_the_receiver()
    {
        var protector = new EphemeralDataProtectionProvider().CreateProtector("p");

        protector.GetUnpartitioned().ShouldBeSameAs(protector);
    }

    [Fact]
    public void get_unpartitioned_on_provider_source_returns_its_unpartitioned_provider()
    {
        var inner = new EphemeralDataProtectionProvider();
        IDataProtectionProvider wrapper = new Wrapper(inner);

        wrapper.GetUnpartitioned().ShouldBeSameAs(inner);
    }

    [Fact]
    public void get_unpartitioned_on_protector_source_returns_its_unpartitioned_protector()
    {
        var inner = new EphemeralDataProtectionProvider().CreateProtector("p");
        IDataProtector wrapper = new ProtectorWrapper(inner);

        wrapper.GetUnpartitioned().ShouldBeSameAs(inner);
    }

    [Fact]
    public void get_unpartitioned_on_protector_source_returning_a_non_protector_throws()
    {
        IDataProtector wrapper = new BadProtectorWrapper();

        Should.Throw<InvalidOperationException>(() => wrapper.GetUnpartitioned())
            .Message.ShouldContain("is not an IDataProtector");
    }

    [Fact]
    public void get_unpartitioned_on_null_provider_throws_argument_null()
    {
        IDataProtectionProvider? provider = null;

        Should.Throw<ArgumentNullException>(() => provider!.GetUnpartitioned());
    }

    private sealed class Wrapper(IDataProtectionProvider inner) : IDataProtectionProvider, IUnpartitionedDataProtectionSource
    {
        public IDataProtector CreateProtector(string purpose) => inner.CreateProtector(purpose);

        public IDataProtectionProvider GetUnpartitioned() => inner;
    }

    private sealed class ProtectorWrapper(IDataProtector inner) : IDataProtector, IUnpartitionedDataProtectionSource
    {
        public IDataProtector CreateProtector(string purpose) => inner.CreateProtector(purpose);

        public byte[] Protect(byte[] plaintext) => inner.Protect(plaintext);

        public byte[] Unprotect(byte[] protectedData) => inner.Unprotect(protectedData);

        public IDataProtectionProvider GetUnpartitioned() => inner;
    }

    private sealed class BadProtectorWrapper : IDataProtector, IUnpartitionedDataProtectionSource
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => plaintext;

        public byte[] Unprotect(byte[] protectedData) => protectedData;

        public IDataProtectionProvider GetUnpartitioned() => new EphemeralDataProtectionProvider();
    }
}
