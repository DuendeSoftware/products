// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;

namespace Duende.IdentityServer.OutsideIn.Harness;

/// <summary>
/// Wraps the single <see cref="FakeTimeProvider" /> shared by every host in an environment (design doc D15).
/// Advancing it moves time for every node, load balancer, client app and the browser's cookie jar at once.
/// </summary>
/// <remarks>
/// The clock starts at the current wall-clock time, rounded down to a whole second, not at a fixed date: IdentityServer
/// and Microsoft.IdentityModel validate some token lifetimes against the wall clock rather than <see cref="TimeProvider" />,
/// so a fixed date would make those tokens look expired. Snapshots therefore show times relative to <see cref="Start" />
/// (see <see cref="Describe" />).
/// </remarks>
public sealed class TestClock
{
    public TestClock()
    {
        Start = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        TimeProvider = new FakeTimeProvider(Start);
    }

    /// <summary>The instant the environment started at.</summary>
    public DateTimeOffset Start { get; }

    /// <summary>The <see cref="TimeProvider" /> registered as <c>TimeProvider</c> in every host's DI container.</summary>
    public FakeTimeProvider TimeProvider { get; }

    public void Advance(TimeSpan delta) => TimeProvider.Advance(delta);

    public void SetUtcNow(DateTimeOffset now) => TimeProvider.SetUtcNow(now);

    /// <summary>Describes an instant relative to <see cref="Start" />, for example <c>{Start}</c> or <c>{Start+15m}</c>.</summary>
    public string Describe(DateTimeOffset instant)
    {
        var offset = instant - Start;
        if (offset == TimeSpan.Zero)
        {
            return "{Start}";
        }

        var builder = new StringBuilder("{Start").Append(offset < TimeSpan.Zero ? '-' : '+');
        var remaining = offset.Duration();
        Append(builder, remaining.Days, 'd');
        Append(builder, remaining.Hours, 'h');
        Append(builder, remaining.Minutes, 'm');
        Append(builder, remaining.Seconds, 's');
        return builder.Append('}').ToString();
    }

    private static void Append(StringBuilder builder, int value, char unit)
    {
        if (value != 0)
        {
            builder.Append(value).Append(unit);
        }
    }
}
