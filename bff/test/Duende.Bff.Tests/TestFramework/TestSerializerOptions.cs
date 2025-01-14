// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;

namespace Duende.Bff.Tests.TestFramework;

public static class TestSerializerOptions
{
    public static JsonSerializerOptions Default => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };
}
