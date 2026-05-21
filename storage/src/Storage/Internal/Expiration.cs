// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// Represents an expiration policy for a store entity.
/// Use <see cref="AtAbsolute"/> for a fixed point in time,
/// <see cref="InRelative"/> for a duration from now,
/// or <see cref="NoExpiration"/> to explicitly indicate no expiration.
/// </summary>
public abstract record Expiration
{
    // Prevent external subclassing.
    private Expiration() { }

    /// <summary>
    /// Resolves this expiration to an absolute <see cref="DateTimeOffset"/> (UTC),
    /// or <c>null</c> if the entity should never expire.
    /// </summary>
    /// <param name="timeProvider">The time provider used to determine the current time for relative expirations.</param>
    /// <returns>The absolute expiration time, or <c>null</c> if the entity should never expire.</returns>
    public abstract DateTimeOffset? Resolve(TimeProvider timeProvider);

    /// <summary>
    /// Creates an expiration at a specific absolute point in time.
    /// </summary>
    /// <param name="expiresAt">The absolute expiration time. Must have <see cref="DateTimeOffset.Offset"/> of <see cref="TimeSpan.Zero"/> (UTC).</param>
    /// <returns>An <see cref="AbsoluteExpiration"/> instance.</returns>
    public static Expiration AtAbsolute(DateTimeOffset expiresAt) => new AbsoluteExpiration(expiresAt);

    /// <summary>
    /// Creates an expiration relative to the current time.
    /// </summary>
    /// <param name="lifetime">The duration from now until expiration. Must be strictly positive.</param>
    /// <returns>A <see cref="RelativeExpiration"/> instance.</returns>
    public static Expiration InRelative(TimeSpan lifetime) => new RelativeExpiration(lifetime);

    /// <summary>
    /// A sentinel value indicating the entity should never expire.
    /// On Create, this means the entity lives forever.
    /// On Update, this explicitly clears any existing expiration.
    /// </summary>
    public static readonly Expiration NoExpiration = new NeverExpiration();

    /// <summary>
    /// An expiration at a fixed absolute point in time (UTC).
    /// </summary>
    internal sealed record AbsoluteExpiration : Expiration
    {
        public DateTimeOffset ExpiresAt { get; }

        public AbsoluteExpiration(DateTimeOffset expiresAt)
        {
            if (expiresAt.Offset != TimeSpan.Zero)
            {
                throw new ArgumentException(
                    "Expiration must be in UTC (Offset must be TimeSpan.Zero).",
                    nameof(expiresAt));
            }

            ExpiresAt = expiresAt;
        }

        public override DateTimeOffset? Resolve(TimeProvider timeProvider) => ExpiresAt;
    }

    /// <summary>
    /// An expiration relative to the current time.
    /// </summary>
    internal sealed record RelativeExpiration : Expiration
    {
        public TimeSpan Lifetime { get; }

        public RelativeExpiration(TimeSpan lifetime)
        {
            if (lifetime <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lifetime),
                    lifetime,
                    "Lifetime must be strictly positive.");
            }

            Lifetime = lifetime;
        }

        public override DateTimeOffset? Resolve(TimeProvider timeProvider) =>
            timeProvider.GetUtcNow() + Lifetime;
    }

    /// <summary>
    /// Sentinel indicating no expiration. Resolves to <c>null</c>.
    /// </summary>
    internal sealed record NeverExpiration : Expiration
    {
        public override DateTimeOffset? Resolve(TimeProvider timeProvider) => null;
    }
}
