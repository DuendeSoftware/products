// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Duende.Bff.Tests.TestInfra;

public static class ConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddJsonString(this IConfigurationBuilder config, string value) => config.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(value)));

    public static IConfigurationBuilder AddJson(this IConfigurationBuilder config, object value) => config.AddJsonString(JsonSerializer.Serialize(value, new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    }));
}
