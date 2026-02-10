// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duende.IdentityServer;

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

    public static T FromString<T>(string value) => JsonSerializer.Deserialize<T>(value, Options);
}
