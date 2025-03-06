// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duende.IdentityServer.Extensions;

#pragma warning disable 1591

namespace Duende.IdentityServer.Stores.Serialization;

public class ClaimsPrincipalConverter : JsonConverter<ClaimsPrincipal>
{
    public override ClaimsPrincipal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var source = JsonSerializer.Deserialize<ClaimsPrincipalLite>(ref reader, options);
        return source?.ToClaimsPrincipal();
    }

    public override void Write(Utf8JsonWriter writer, ClaimsPrincipal value, JsonSerializerOptions options)
    {
        var target = value.ToClaimsPrincipalLite();
        JsonSerializer.Serialize(writer, target, options);
    }
}