// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using Duende.Spaces.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace Duende.Spaces.DataProtection;

public sealed class SpacePartitionedDataProtectorTests : IDisposable
{
    private static readonly byte[] Data = [1, 2, 3, 4];

    private IDataProtectionProvider _root = new EphemeralDataProtectionProvider();
    private ServiceProvider _sp = null!;
    private ISpaceContextAccessor _accessor = null!;
    private IDataProtectionProvider _provider = null!;
    private MetricCollector<long> _fallbacks = null!;

    public SpacePartitionedDataProtectorTests() => UseRoot(_root);

    public void Dispose()
    {
        _fallbacks.Dispose();
        _sp.Dispose();
    }

    private static readonly SpaceId Named = SpaceId.New();

    public static TheoryData<SpaceId, SpaceId, string> CrossSpacePairs() => new()
    {
        { SpaceId.Default, Named, "default to named" },
        { Named, SpaceId.Default, "named to default" },
        { SpaceId.Management, SpaceId.Default, "management to default" },
        { SpaceId.Default, SpaceId.Management, "default to management" },
        { SpaceId.Management, Named, "management to named" },
        { Named, SpaceId.Management, "named to management" },
    };

    public static TheoryData<string> DangerousOrNot => new() { "Unprotect", "DangerousUnprotect" };

    private long FallbackCount => _fallbacks.GetMeasurementSnapshot().Sum(m => m.Value);

    private void UseRoot(IDataProtectionProvider root)
    {
        _fallbacks?.Dispose();
        _sp?.Dispose();
        _root = root;
        var services = new ServiceCollection();
        services.AddLogging();
        TestSpacesLicense.RegisterEntitled(services);
        services.AddSingleton(root);
        services.AddSpacesInternal();
        _sp = services.BuildServiceProvider();
        new ApplicationBuilder(_sp).UseSpaceResolution();
        _accessor = _sp.GetRequiredService<ISpaceContextAccessor>();
        _provider = _sp.GetRequiredService<IDataProtectionProvider>();
        _fallbacks = new MetricCollector<long>(
            _sp.GetRequiredService<IMeterFactory>(),
            "Duende.Spaces",
            "duende.spaces.data_protection.default_space_fallback");
    }

    private static byte[] Run(IDataProtector protector, string operation) =>
        operation == "Unprotect"
            ? protector.Unprotect(Data)
            : ((IPersistedDataProtector)protector).DangerousUnprotect(Data, false, out _, out _);

    [Theory]
    [MemberData(nameof(CrossSpacePairs))]
    public void value_protected_in_one_space_is_rejected_in_another(SpaceId from, SpaceId to, string motivation)
    {
        var sut = _provider.CreateProtector("p");
        byte[] protectedData;
        using (_accessor.SetSpace(from))
        {
            protectedData = sut.Protect(Data);
        }

        using (_accessor.SetSpace(to))
        {
            Should.Throw<CryptographicException>(() => sut.Unprotect(protectedData), motivation);
        }
    }

    [Fact]
    public void one_protector_reused_across_sequential_scopes_stays_isolated()
    {
        var sut = _provider.CreateProtector("p");
        var a = SpaceId.New();
        var b = SpaceId.New();
        byte[] inA;
        using (_accessor.SetSpace(a))
        {
            inA = sut.Protect(Data);
            sut.Unprotect(inA).ShouldBe(Data);
        }

        using (_accessor.SetSpace(b))
        {
            var inB = sut.Protect(Data);
            Should.Throw<CryptographicException>(() => sut.Unprotect(inA));
            sut.Unprotect(inB).ShouldBe(Data);
        }

        using (_accessor.SetSpace(a))
        {
            sut.Unprotect(inA).ShouldBe(Data);
        }
    }

    [Fact]
    public void protect_unprotect_and_dangerous_unprotect_fail_closed_with_guidance_when_no_space_is_resolved()
    {
        var sut = _provider.CreateProtector("p");
        var persisted = (IPersistedDataProtector)sut;

        var failures = new[]
        {
            Should.Throw<InvalidOperationException>(() => sut.Protect(Data)),
            Should.Throw<InvalidOperationException>(() => sut.Unprotect(Data)),
            Should.Throw<InvalidOperationException>(() => persisted.DangerousUnprotect(Data, false, out _, out _)),
        };

        foreach (var ex in failures)
        {
            ex.Message.ShouldContain("UseSpaceResolution()");
            ex.Message.ShouldContain("SetSpace");
            ex.Message.ShouldContain("GetUnpartitioned()");
        }
    }

    [Fact]
    public void default_space_output_is_rejected_by_root_with_unpartitioned_chain()
    {
        var sut = _provider.CreateProtector("p1").CreateProtector("p2");
        byte[] protectedData;
        using (_accessor.SetSpace(SpaceId.Default))
        {
            protectedData = sut.Protect(Data);
        }

        Should.Throw<CryptographicException>(() => ((IDataProtector)sut.GetUnpartitioned()).Unprotect(protectedData));
        Should.Throw<CryptographicException>(() => _root.CreateProtector("p1").CreateProtector("p2").Unprotect(protectedData));
    }

    [Fact]
    public void get_unpartitioned_on_nested_protector_round_trips_with_root_chain()
    {
        var sut = _provider.CreateProtector("p1").CreateProtector("p2");

        var protectedData = sut.GetUnpartitioned().Protect(Data);

        _root.CreateProtector("p1").CreateProtector("p2").Unprotect(protectedData).ShouldBe(Data);
    }

    [Fact]
    public void time_limited_protector_over_partitioned_protector_stays_partitioned()
    {
        var sut = _provider.CreateProtector("p").ToTimeLimitedDataProtector();
        byte[] protectedData;
        using (_accessor.SetSpace(SpaceId.New()))
        {
            protectedData = sut.Protect(Data, TimeSpan.FromMinutes(5));
            sut.Unprotect(protectedData).ShouldBe(Data);
        }

        using (_accessor.SetSpace(SpaceId.New()))
        {
            Should.Throw<CryptographicException>(() => sut.Unprotect(protectedData));
        }
    }

    [Fact]
    public void unpartitioned_value_is_accepted_in_default_space_and_records_the_fallback_metric_once()
    {
        var protectedData = _root.CreateProtector("p1").CreateProtector("p2").Protect(Data);
        var sut = _provider.CreateProtector("p1").CreateProtector("p2");
        using var scope = _accessor.SetSpace(SpaceId.Default);

        sut.Unprotect(protectedData).ShouldBe(Data);

        var measurement = _fallbacks.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags.Count.ShouldBe(1);
        measurement.Tags["duende.spaces.data_protection.operation"].ShouldBe("unprotect");
    }

    [Theory]
    [InlineData("Management")]
    [InlineData("Named")]
    public void unpartitioned_value_is_rejected_outside_the_default_space(string space)
    {
        var protectedData = _root.CreateProtector("p").Protect(Data);
        var sut = _provider.CreateProtector("p");
        using var scope = _accessor.SetSpace(space == "Management" ? SpaceId.Management : SpaceId.New());

        Should.Throw<CryptographicException>(() => sut.Unprotect(protectedData));
        FallbackCount.ShouldBe(0);
    }

    [Fact]
    public void tampered_payload_in_default_space_still_throws_without_recording_the_fallback_metric()
    {
        var sut = _provider.CreateProtector("p");
        using var scope = _accessor.SetSpace(SpaceId.Default);

        Should.Throw<CryptographicException>(() => sut.Unprotect([.. Enumerable.Range(1, 30).Select(i => (byte)i)]));
        FallbackCount.ShouldBe(0);
    }

    [Fact]
    public void partitioned_value_is_read_in_default_space_without_recording_the_fallback_metric()
    {
        var sut = _provider.CreateProtector("p");
        using var scope = _accessor.SetSpace(SpaceId.Default);

        sut.Unprotect(sut.Protect(Data)).ShouldBe(Data);

        FallbackCount.ShouldBe(0);
    }

    [Fact]
    public void value_protected_via_get_unpartitioned_with_shared_purposes_is_accepted_only_in_default_space()
    {
        var sut = _provider.CreateProtector("p1").CreateProtector("p2");
        var protectedData = sut.GetUnpartitioned().Protect(Data);

        using (_accessor.SetSpace(SpaceId.Default))
        {
            sut.Unprotect(protectedData).ShouldBe(Data);
        }

        using (_accessor.SetSpace(SpaceId.Management))
        {
            Should.Throw<CryptographicException>(() => sut.Unprotect(protectedData));
        }

        using (_accessor.SetSpace(SpaceId.New()))
        {
            Should.Throw<CryptographicException>(() => sut.Unprotect(protectedData));
        }
    }

    [Fact]
    public void get_unpartitioned_value_is_readable_in_every_space()
    {
        var sut = _provider.CreateProtector("p");
        var protectedData = sut.GetUnpartitioned().Protect(Data);

        foreach (var space in new[] { SpaceId.Default, SpaceId.Management, SpaceId.New() })
        {
            using (_accessor.SetSpace(space))
            {
                sut.GetUnpartitioned().Unprotect(protectedData).ShouldBe(Data);
            }
        }
    }

    [Fact]
    public void dangerous_unprotect_round_trips_in_a_named_space_without_migration_or_revocation()
    {
        var sut = (IPersistedDataProtector)_provider.CreateProtector("p");
        using var scope = _accessor.SetSpace(SpaceId.New());

        var protectedData = ((IDataProtector)sut).Protect(Data);

        sut.DangerousUnprotect(protectedData, false, out var requiresMigration, out var wasRevoked).ShouldBe(Data);
        requiresMigration.ShouldBeFalse();
        wasRevoked.ShouldBeFalse();
    }

    [Fact]
    public void dangerous_unprotect_of_unpartitioned_value_in_default_space_requires_migration_and_records_the_fallback_metric()
    {
        var protectedData = _root.CreateProtector("p").Protect(Data);
        var sut = (IPersistedDataProtector)_provider.CreateProtector("p");
        using var scope = _accessor.SetSpace(SpaceId.Default);

        sut.DangerousUnprotect(protectedData, false, out var requiresMigration, out _).ShouldBe(Data);

        requiresMigration.ShouldBeTrue();
        var measurement = _fallbacks.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags.Count.ShouldBe(1);
        measurement.Tags["duende.spaces.data_protection.operation"].ShouldBe("dangerous_unprotect");
    }

    [Fact]
    public void dangerous_unprotect_of_unpartitioned_value_in_a_named_space_is_rejected()
    {
        var protectedData = _root.CreateProtector("p").Protect(Data);
        var sut = (IPersistedDataProtector)_provider.CreateProtector("p");
        using var scope = _accessor.SetSpace(SpaceId.New());

        Should.Throw<CryptographicException>(() => sut.DangerousUnprotect(protectedData, false, out _, out _));
        FallbackCount.ShouldBe(0);
    }

    [Fact]
    public void failed_default_space_fallback_rethrows_the_original_partitioned_exception()
    {
        var scripted = new ScriptedDataProtectionProvider();
        UseRoot(scripted);
        var sut = _provider.CreateProtector("p");
        using var scope = _accessor.SetSpace(SpaceId.Default);

        var thrown = Should.Throw<CryptographicException>(() => sut.Unprotect(Data));

        thrown.ShouldBeSameAs(scripted.Partitioned().Thrown);
        thrown.ShouldNotBeSameAs(scripted.Unpartitioned("p").Thrown);
        FallbackCount.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(DangerousOrNot))]
    public void non_cryptographic_exception_from_the_partitioned_protector_propagates_without_fallback(string operation)
    {
        var scripted = new ScriptedDataProtectionProvider { ThrowInvalidOperation = _ => true };
        UseRoot(scripted);
        var sut = _provider.CreateProtector("p");
        using var scope = _accessor.SetSpace(SpaceId.Default);

        var ex = Should.Throw<InvalidOperationException>(() => Run(sut, operation));

        ex.Message.ShouldBe("inner failure");
        scripted.Partitioned().UnprotectCalls.ShouldBe(operation == "Unprotect" ? 1 : 0);
        scripted.Partitioned().DangerousCalls.ShouldBe(operation == "Unprotect" ? 0 : 1);
        scripted.Created.Where(c => c.Chain.SequenceEqual(["p"])).Sum(c => c.UnprotectCalls + c.DangerousCalls).ShouldBe(0);
        FallbackCount.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(DangerousOrNot))]
    public void non_cryptographic_exception_from_the_unpartitioned_fallback_rethrows_the_original_partitioned_exception(string operation)
    {
        var scripted = new ScriptedDataProtectionProvider { ThrowInvalidOperation = chain => chain[0] == "p" };
        UseRoot(scripted);
        var sut = _provider.CreateProtector("p");
        using var scope = _accessor.SetSpace(SpaceId.Default);

        var thrown = Should.Throw<CryptographicException>(() => Run(sut, operation));

        thrown.ShouldBeSameAs(scripted.Partitioned().Thrown);
        var fallback = scripted.Unpartitioned("p");
        (fallback.UnprotectCalls + fallback.DangerousCalls).ShouldBe(1);
        FallbackCount.ShouldBe(0);
    }

    [Fact]
    public void dangerous_unprotect_fallback_forces_requires_migration_even_when_the_inner_protector_reports_false()
    {
        var scripted = new ScriptedDataProtectionProvider
        {
            Succeeds = chain => chain[0] == "p",
            RequiresMigration = _ => false,
        };
        UseRoot(scripted);
        var sut = (IPersistedDataProtector)_provider.CreateProtector("p");
        using var scope = _accessor.SetSpace(SpaceId.Default);

        sut.DangerousUnprotect(Data, false, out var requiresMigration, out _);

        requiresMigration.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void dangerous_unprotect_preserves_was_revoked_from_the_inner_protector(bool viaFallback)
    {
        var scripted = new ScriptedDataProtectionProvider
        {
            Succeeds = chain => chain[0] == (viaFallback ? "p" : "space"),
            WasRevoked = _ => true,
        };
        UseRoot(scripted);
        var sut = (IPersistedDataProtector)_provider.CreateProtector("p");
        using var scope = _accessor.SetSpace(viaFallback ? SpaceId.Default : SpaceId.New());

        sut.DangerousUnprotect(Data, true, out var requiresMigration, out var wasRevoked);

        wasRevoked.ShouldBeTrue();
        requiresMigration.ShouldBe(viaFallback);
    }

    [Fact]
    public void dangerous_unprotect_in_management_space_never_calls_the_unpartitioned_protector()
    {
        var scripted = new ScriptedDataProtectionProvider { Succeeds = chain => chain[0] == "p" };
        UseRoot(scripted);
        var sut = (IPersistedDataProtector)_provider.CreateProtector("p");
        using var scope = _accessor.SetSpace(SpaceId.Management);

        Should.Throw<CryptographicException>(() => sut.DangerousUnprotect(Data, false, out _, out _));

        scripted.Created.Where(c => c.Chain[0] == "p").Sum(c => c.DangerousCalls + c.UnprotectCalls).ShouldBe(0);
        FallbackCount.ShouldBe(0);
    }
}
