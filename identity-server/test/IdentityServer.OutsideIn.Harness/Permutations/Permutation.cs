// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Xunit.Sdk;

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// One configuration a test runs under: a value for every <see cref="Dimensions" /> flag (design doc D34). Produced by
/// <see cref="MatrixAttribute" /> and passed to <c>TestEnvironment.StartAsync</c>.
/// </summary>
public sealed class Permutation : IXunitSerializable
{
    /// <summary>For xunit serialization only.</summary>
    [Obsolete("Called by xunit to deserialize a permutation. Use the other constructor.", error: true)]
    public Permutation()
    {
    }

    public Permutation(StoreType store, CachingMode caching, UserStore users, SpacesMode spaces)
    {
        Store = store;
        Caching = caching;
        Users = users;
        Spaces = spaces;
    }

    /// <summary>The permutation every undeclared dimension falls back to.</summary>
    public static Permutation Default { get; } = new(StoreType.InMemory, CachingMode.None, UserStore.TestUsers, SpacesMode.Off);

    public StoreType Store { get; private set; }

    public CachingMode Caching { get; private set; }

    public UserStore Users { get; private set; }

    public SpacesMode Spaces { get; private set; }

    /// <summary>
    /// Whether D32 allows this combination: Spaces and User Management only work on Duende.Storage, for now.
    /// </summary>
    public bool IsValid =>
        IsDuendeStorage || (Users == UserStore.TestUsers && Spaces == SpacesMode.Off);

    internal bool IsDuendeStorage =>
        Store is StoreType.DuendeStorageSchemasInMemory or StoreType.DuendeStorageSchemasInDatabase;

    /// <summary>
    /// Why this permutation can't run yet, or <c>null</c> when every value in it is implemented. Every value of every
    /// dimension (stores, caching, users and Spaces) is implemented in this slice of the harness, so this is always
    /// <c>null</c>. Kept so <see cref="MatrixAttribute" /> and <see cref="PermutationMatrix" /> have a place to report
    /// a future dimension value that isn't implemented yet, without changing their shape.
    /// </summary>
    internal string? NotImplementedReason => null;

    public override string ToString() => $"Store={Store}, Caching={Caching}, Users={Users}, Spaces={Spaces}";

    void IXunitSerializable.Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(Store), Store);
        info.AddValue(nameof(Caching), Caching);
        info.AddValue(nameof(Users), Users);
        info.AddValue(nameof(Spaces), Spaces);
    }

    void IXunitSerializable.Deserialize(IXunitSerializationInfo info)
    {
        Store = info.GetValue<StoreType>(nameof(Store));
        Caching = info.GetValue<CachingMode>(nameof(Caching));
        Users = info.GetValue<UserStore>(nameof(Users));
        Spaces = info.GetValue<SpacesMode>(nameof(Spaces));
    }
}
