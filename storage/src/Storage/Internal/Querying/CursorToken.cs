// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using System.Text.Json;

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Represents a decoded cursor token containing the last-seen position for seek-based pagination.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
internal sealed record CursorToken
{
    /// <summary>
    /// The ID of the last entity on the previous page.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The sort field value of the last entity on the previous page (string type).
    /// </summary>
    public string? StringValue { get; init; }

    /// <summary>
    /// The sort field value of the last entity on the previous page (number type).
    /// </summary>
    public decimal? NumberValue { get; init; }

    /// <summary>
    /// The sort field value of the last entity on the previous page (datetime type).
    /// </summary>
    public DateTimeOffset? DateTimeValue { get; init; }

    /// <summary>
    /// The sort field value of the last entity on the previous page (boolean type).
    /// </summary>
    public bool? BooleanValue { get; init; }

    /// <summary>
    /// The sort field value of the last entity on the previous page (guid type).
    /// </summary>
    public Guid? GuidValue { get; init; }

    /// <summary>
    /// Encodes the cursor token to an opaque base64 string.
    /// </summary>
    public string Encode()
    {
        var json = JsonSerializer.Serialize(this);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Decodes an opaque base64 cursor token string.
    /// </summary>
    /// <param name="token">The encoded token string.</param>
    /// <returns>The decoded cursor token, or null if the token is invalid.</returns>
    public static CursorToken? Decode(string token)
    {
        try
        {
            var bytes = Convert.FromBase64String(token);
            var json = Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<CursorToken>(json);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a cursor token from the last item's sort value and ID.
    /// </summary>
    public static CursorToken Create(
        Guid id,
        string? stringValue,
        decimal? numberValue,
        DateTimeOffset? dateTimeValue,
        bool? booleanValue,
        Guid? guidValue) =>
        new()
        {
            Id = id,
            StringValue = stringValue,
            NumberValue = numberValue,
            DateTimeValue = dateTimeValue,
            BooleanValue = booleanValue,
            GuidValue = guidValue
        };
}
