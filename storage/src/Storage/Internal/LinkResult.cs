// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal;

/// <summary>
/// The result of a Link operation on <see cref="IStore"/>.
/// </summary>
public enum LinkResult
{
    /// <summary>The link was created successfully.</summary>
    Success,

    /// <summary>The exact same link already exists (idempotent — not an error, but callers can detect duplicates).</summary>
    AlreadyLinked,
}
