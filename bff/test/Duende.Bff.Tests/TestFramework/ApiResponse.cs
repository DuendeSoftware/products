// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using Shouldly;

namespace Duende.Bff.Tests.TestFramework
{
    public record ApiResponse(string Method, string Path, string? Sub, string? ClientId, IEnumerable<ClaimRecord> Claims)
    {
        public required string? Body { get; init; }

        public Dictionary<string, List<string>> RequestHeaders { get; init; } = new();

        public T BodyAs<T>()
        {
            Body.ShouldNotBeNull();
            return JsonSerializer.Deserialize<T>(Body, TestSerializerOptions.Default) ?? throw new NullReferenceException($"result {Body} could not be deserialized to {typeof(T).Name}");
        }
    }
}
