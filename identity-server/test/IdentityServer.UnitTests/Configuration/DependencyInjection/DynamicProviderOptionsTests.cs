// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Duende.IdentityServer.Configuration;

namespace UnitTests.Configuration.DependencyInjection;

public class DynamicProviderOptionsTests
{
    private readonly DynamicProviderOptions _subject = new();

    [Fact]
    public void CacheIdleTimeout_defaults_to_one_hour() => _subject.CacheIdleTimeout.ShouldBe(TimeSpan.FromHours(1));

    [Fact]
    public void CacheIdleTimeout_accepts_infinite_timeout() => Should.NotThrow(() => _subject.CacheIdleTimeout = Timeout.InfiniteTimeSpan);

    [Fact]
    public void CacheIdleTimeout_rejects_zero() => Should.Throw<ArgumentOutOfRangeException>(() => _subject.CacheIdleTimeout = TimeSpan.Zero);

    [Fact]
    public void CacheIdleTimeout_rejects_a_negative_value() => Should.Throw<ArgumentOutOfRangeException>(() => _subject.CacheIdleTimeout = TimeSpan.FromMinutes(-1));
}
