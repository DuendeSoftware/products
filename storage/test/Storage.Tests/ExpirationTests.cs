// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.Storage;

public sealed class ExpirationTests
{
    [Fact]
    public void Absolute_expiration_resolve_should_return_expires_at()
    {
        var expiresAt = new DateTimeOffset(2030, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var expiration = Expiration.AtAbsolute(expiresAt);

        var resolved = expiration.Resolve(TimeProvider.System);

        resolved.ShouldBe(expiresAt);
    }

    [Fact]
    public void Relative_expiration_resolve_should_return_now_plus_lifetime()
    {
        var tp = new FakeTimeProvider(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var lifetime = TimeSpan.FromHours(2);
        var expiration = Expiration.InRelative(lifetime);

        var resolved = expiration.Resolve(tp);

        resolved.ShouldBe(tp.GetUtcNow() + lifetime);
    }

    [Fact]
    public void Never_expiration_resolve_should_return_null()
    {
        var resolved = Expiration.NoExpiration.Resolve(TimeProvider.System);

        resolved.ShouldBeNull();
    }

    [Fact]
    public void Absolute_expiration_non_utc_offset_should_throw_ArgumentException()
    {
        var nonUtc = new DateTimeOffset(2030, 6, 15, 12, 0, 0, TimeSpan.FromHours(5));

        _ = Should.Throw<ArgumentException>(() => Expiration.AtAbsolute(nonUtc));
    }

    [Fact]
    public void Relative_expiration_zero_TimeSpan_should_throw_ArgumentOutOfRangeException() => Should.Throw<ArgumentOutOfRangeException>(() => Expiration.InRelative(TimeSpan.Zero));

    [Fact]
    public void Relative_expiration_negative_TimeSpan_should_throw_ArgumentOutOfRangeException() => Should.Throw<ArgumentOutOfRangeException>(() => Expiration.InRelative(TimeSpan.FromMinutes(-5)));

    [Fact]
    public void No_expiration_should_be_singleton()
    {
        var a = Expiration.NoExpiration;
        var b = Expiration.NoExpiration;

        ReferenceEquals(a, b).ShouldBeTrue();
    }
}
