// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage;

public sealed record SchemaVerificationResult(IReadOnlyList<SchemaVerificationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
