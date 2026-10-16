// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Duende.Platform.UserManagement.Passkeys;

public sealed class SignatureVerifiersTests
{
    [Fact]
    public void All_orders_built_ins_before_additional_in_registration_order()
    {
        var builtIn1 = new Es256SignatureVerifier();
        var builtIn2 = new Rs256SignatureVerifier();
        var additional1 = new FakeSignatureVerifier(1);
        var additional2 = new FakeSignatureVerifier(2);

        var verifiers = new SignatureVerifiers(
            new BuiltInSignatureVerifiers([builtIn1, builtIn2]),
            [additional1, additional2]);

        verifiers.All.ShouldBe<ISignatureVerifier>([builtIn1, builtIn2, additional1, additional2]);
    }

    [Fact]
    public async Task Startup_validation_and_ceremonies_share_one_verifier_set()
    {
        // A transient registration would otherwise give validation and ceremonies different instances.
        await using var serviceProvider = await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: null,
            addDataProtection: false,
            configureServices: services => _ = services.AddTransient<ISignatureVerifier, EdDsaSignatureVerifier>());

        // Reading the options runs UserAuthenticationOptionsValidator, which resolves the verifier set.
        _ = serviceProvider.GetRequiredService<IOptions<UserAuthenticationOptions>>().Value;
        var validated = serviceProvider.GetRequiredService<SignatureVerifiers>();
        var usedByCeremony = serviceProvider.GetRequiredService<SignatureVerifiers>();

        usedByCeremony.ShouldBeSameAs(validated);
        usedByCeremony.All.OfType<EdDsaSignatureVerifier>().Single()
            .ShouldBeSameAs(validated.All.OfType<EdDsaSignatureVerifier>().Single());
    }

    private sealed class FakeSignatureVerifier(int algorithm) : ISignatureVerifier
    {
        public int Algorithm => algorithm;
        public bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[]? signature) => true;
    }

#pragma warning disable CA1812 // Instantiated through DI
    private sealed class EdDsaSignatureVerifier : ISignatureVerifier
#pragma warning restore CA1812
    {
        public int Algorithm => -8;
        public bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[]? signature) => false;
    }
}
