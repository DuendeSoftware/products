// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;

namespace Duende.IdentityServer.Saml.Common;

#nullable enable

/// <summary>
/// DateTime that only allows DateTimeKind UTC.
/// </summary>
[Serializable]
public readonly struct DateTimeUtc : IEquatable<DateTimeUtc>
{
    /// <summary>
    /// Ticks of the DateTime
    /// </summary>
    public long Ticks { get; init; }

    /// <summary>
    /// Construct a DateTimeUtc from ticks
    /// </summary>
    /// <param name="ticks"></param>
    public DateTimeUtc(long ticks) => Ticks = ticks;

    /// <summary>
    /// Construct a DateTimeUtc
    /// </summary>
    /// <param name="year">Year</param>
    /// <param name="month">Month</param>
    /// <param name="day">Day</param>
    /// <param name="hour">Hour</param>
    /// <param name="minute">Minute</param>
    /// <param name="second">Second</param>
    public DateTimeUtc(int year, int month, int day, int hour, int minute, int second)
    {
        DateTime dt = new(year, month, day, hour, minute, second, DateTimeKind.Utc);

        Ticks = dt.Ticks;
    }

    /// <summary>
    /// Implicit conversion from DateTime, validates that the
    /// source DateTimeKind is Utc.
    /// </summary>
    /// <param name="source">Source DateTime</param>
#pragma warning disable CA1065 // Do not raise exceptions in unexpected locations — validation of UTC kind is intentional
    public static implicit operator DateTimeUtc(DateTime source)
    {
        if (source.Kind != DateTimeKind.Utc
            && (source.Ticks != 0 || source.Kind != DateTimeKind.Unspecified))
        {
            throw new ArgumentException("DateTime must be of Utc kind");
        }
        return new DateTimeUtc(source.Ticks);
    }
#pragma warning restore CA1065

    /// <summary>
    /// Implicit conversion to DateTime.
    /// </summary>
    /// <param name="source">Source DateTimeUtc</param>
    public static implicit operator DateTime(DateTimeUtc source) => new(source.Ticks, DateTimeKind.Utc);

    /// <summary>
    /// Implicit conversion to DateTimeOffset
    /// </summary>
    /// <param name="source">Source DateTimeUtc</param>
    public static implicit operator DateTimeOffset(DateTimeUtc source)
        => new(source);

    /// <summary>
    /// Creates a <see cref="DateTimeUtc"/> from a <see cref="DateTime"/>.
    /// The source must be of <see cref="DateTimeKind.Utc"/> kind.
    /// </summary>
    /// <param name="source">Source DateTime</param>
    /// <returns>A new DateTimeUtc</returns>
    public static DateTimeUtc FromDateTime(DateTime source) => source;

    /// <summary>
    /// Converts this instance to a <see cref="DateTime"/> with <see cref="DateTimeKind.Utc"/>.
    /// </summary>
    /// <returns>A DateTime</returns>
    public DateTime ToDateTime() => this;

    /// <summary>
    /// Converts this instance to a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <returns>A DateTimeOffset</returns>
    public DateTimeOffset ToDateTimeOffset() => this;

    /// <summary>
    /// Compares this instance to a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="other">The DateTimeOffset to compare to</param>
    /// <returns>Negative if this is earlier, zero if equal, positive if later</returns>
    public int CompareTo(DateTimeOffset other) => ((DateTimeOffset)this).CompareTo(other);

    /// <summary>
    /// Operator less than
    /// </summary>
    /// <param name="dto">DateTimeOffset</param>
    /// <param name="dtu">DateTimeUtc</param>
    /// <returns>Bool result</returns>
    public static bool operator <(DateTimeOffset dto, DateTimeUtc dtu) =>
        dto < (DateTime)dtu;

    /// <summary>
    /// Operator greater than
    /// </summary>
    /// <param name="dto">DateTimeOffset</param>
    /// <param name="dtu">DateTimeUtc</param>
    /// <returns>Bool result</returns>
    public static bool operator >(DateTimeOffset dto, DateTimeUtc dtu) =>
        dto > (DateTime)dtu;

    /// <summary>
    /// Operator greater or equal than
    /// </summary>
    /// <param name="dto">DateTimeOffset</param>
    /// <param name="dtu">DateTimeUtc</param>
    /// <returns>Bool result</returns>
    public static bool operator >=(DateTimeOffset dto, DateTimeUtc dtu) =>
        dto >= (DateTime)dtu;

    /// <summary>
    /// Operator less or equal than
    /// </summary>
    /// <param name="dto">DateTimeOffset</param>
    /// <param name="dtu">DateTimeUtc</param>
    /// <returns>Bool result</returns>
    public static bool operator <=(DateTimeOffset dto, DateTimeUtc dtu) =>
        dto <= (DateTime)dtu;

    /// <summary>
    /// Equality operator
    /// </summary>
    /// <param name="left">Left operand</param>
    /// <param name="right">Right operand</param>
    /// <returns>True if equal</returns>
    public static bool operator ==(DateTimeUtc left, DateTimeUtc right) => left.Ticks == right.Ticks;

    /// <summary>
    /// Inequality operator
    /// </summary>
    /// <param name="left">Left operand</param>
    /// <param name="right">Right operand</param>
    /// <returns>True if not equal</returns>
    public static bool operator !=(DateTimeUtc left, DateTimeUtc right) => left.Ticks != right.Ticks;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DateTimeUtc other && Ticks == other.Ticks;

    /// <inheritdoc/>
    public bool Equals(DateTimeUtc other) => Ticks == other.Ticks;

    /// <inheritdoc/>
    public override int GetHashCode() => Ticks.GetHashCode();

    /// <summary>
    /// ToString
    /// </summary>
    /// <returns>String</returns>
    public override string ToString() => ((DateTime)this).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}
