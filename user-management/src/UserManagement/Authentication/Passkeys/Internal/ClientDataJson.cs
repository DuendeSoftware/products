// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Parsed client data from clientDataJSON.
/// </summary>
internal sealed record ClientDataJson(
    string Type,
    string Challenge,
    string Origin,
    bool? CrossOrigin)
{
    private static class JsonKeys
    {
        public const string Type = "type";
        public const string Challenge = "challenge";
        public const string Origin = "origin";
        public const string CrossOrigin = "crossOrigin";
    }

    internal static bool TryParse(ReadOnlySpan<byte> json, [NotNullWhen(true)] out ClientDataJson? result)
    {
        result = null;

        try
        {
            using var doc = JsonDocument.Parse(json.ToArray());
            var root = doc.RootElement;

            if (!root.TryGetProperty(JsonKeys.Type, out var typeElement) ||
                !root.TryGetProperty(JsonKeys.Challenge, out var challengeElement) ||
                !root.TryGetProperty(JsonKeys.Origin, out var originElement))
            {
                return false;
            }

            var type = typeElement.GetString();
            var challenge = challengeElement.GetString();
            var origin = originElement.GetString();

            if (type is null || challenge is null || origin is null)
            {
                return false;
            }

            bool? crossOrigin = null;
            if (root.TryGetProperty(JsonKeys.CrossOrigin, out var crossOriginElement))
            {
                crossOrigin = crossOriginElement.GetBoolean();
            }

            result = new ClientDataJson(type, challenge, origin, crossOrigin);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
