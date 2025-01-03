// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Generic;

namespace Duende.Bff.Tests.TestFramework
{
    public record ApiResponse(string Method, string Path, string Sub, string ClientId, IEnumerable<ClaimRecord> Claims)
    {
        public string Body { get; init; }

        public Dictionary<string, List<string>> RequestHeaders { get; init; }
    }
}
