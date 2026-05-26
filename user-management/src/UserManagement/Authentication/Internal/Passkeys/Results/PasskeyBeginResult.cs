// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace Duende.UserManagement.Authentication.Internal.Passkeys.Results;

internal sealed class PasskeyBeginResult(Guid challengeId, object options) : IResult
{
    public Guid ChallengeId => challengeId;
    public object Options => options;
    public async Task ExecuteAsync(HttpContext context)
    {
        context.Response.SetNoCache();
        var dto = new ResultDto { ChallengeId = ChallengeId, Options = Options };
        var json = ObjectSerializer.ToString(dto, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await context.Response.WriteJsonAsync(json);
    }
}

internal sealed record ResultDto
{
    public required Guid ChallengeId { get; init; }
    public required object Options { get; init; }
}

public static class HttpResponsePasskeyExtensions
{
    extension(HttpResponse response)
    {
        public void SetNoCache()
        {
            if (!response.Headers.ContainsKey("Cache-Control"))
            {
                response.Headers.Append("Cache-Control", "no-store, no-cache, max-age=0");
            }
            else
            {
                response.Headers.CacheControl = "no-store, no-cache, max-age=0";
            }

            if (!response.Headers.ContainsKey("Pragma"))
            {
                response.Headers.Append("Pragma", "no-cache");
            }
        }

        public async Task WriteJsonAsync(object o)
        {
            var json = ObjectSerializer.ToString(o);
            await response.WriteJsonAsync(json);
        }

        public async Task WriteJsonAsync(object o, string contentType)
        {
            var json = ObjectSerializer.ToString(o);
            await response.WriteJsonAsync(json, contentType);
        }

        public async Task WriteJsonAsync(string json)
        {
            response.ContentType = "application/json; charset=UTF-8";
            await response.WriteAsync(json);
            await response.Body.FlushAsync();
        }

        public async Task WriteJsonAsync(string json, string contentType)
        {
            response.ContentType = contentType;
            await response.WriteAsync(json);
            await response.Body.FlushAsync();
        }
    }
}

internal static class ObjectSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions OptionsWithoutEscaping = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Use UnsafeRelaxedJsonEscaping to avoid escaping '+' as '\u002B' in base64-encoded
        // values like x5c certificates. The '+' character is valid in JSON strings and does
        // not need to be escaped. The default encoder escapes it for HTML safety, but our
        // JSON responses are served with application/json content type.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Serializes an object to a JSON string using the provided <see cref="JsonSerializerOptions"/>,
    /// including any custom encoder or ignore-null settings specified by the caller.
    /// </summary>
    public static string ToString(object o, JsonSerializerOptions options) => JsonSerializer.Serialize(o, options);

    /// <summary>
    /// Serializes an object to a JSON string using default encoding, which escapes
    /// certain characters (such as '+') for HTML safety.
    /// </summary>
    public static string ToString(object o) => JsonSerializer.Serialize(o, Options);

    /// <summary>
    /// Serializes an object to a JSON string using relaxed encoding that does not
    /// escape characters like '+'. This is useful for producing JSON where
    /// base64-encoded values (e.g., x5c certificates) should remain unescaped.
    /// </summary>
    public static string ToUnescapedString(object o) => JsonSerializer.Serialize(o, OptionsWithoutEscaping);

    public static T? FromString<T>(string value) => JsonSerializer.Deserialize<T>(value, Options);
}
