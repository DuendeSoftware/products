// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying;

namespace Duende.Storage.Internal.Pagination;

/// <summary>
/// An opaque continuation token used for cursor-based pagination.
/// Returned in <see cref="QueryResult{T}"/> and consumed by <see cref="DataRange"/>.
/// The token's contents are an implementation detail: callers must not parse or construct one by
/// hand, only pass back a token they previously received, unchanged, with the same filter and sort.
/// A token carries its position in the result set and the direction it was issued for, and is
/// checked only against the entity type and the sort of the query it's used with: using it with a
/// different sort, or a malformed or unrecognized token, throws
/// <see cref="InvalidContinuationTokenException"/>. The filter is not part of that check, so pass
/// back the same filter the token was produced with; querying with a different filter returns rows
/// from the token's position without an error, which usually isn't what the caller wants.
/// A token that was changed by hand isn't always detected as invalid: it can still only move the
/// position within the data the caller's own filter and entity type already allow it to see.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
[StringValue]
public partial record ContinuationToken
{
    /// <summary>
    /// The token value representing the beginning of the dataset. Using it starts at the first page.
    /// An empty string is not a valid token on its own: <c>ContinuationToken.Create("")</c> throws
    /// <see cref="FormatException"/>. To start from the first page, use <see cref="Beginning"/>
    /// instead. Callers that accept tokens from external clients should use
    /// <see cref="TryCreate(string?, out ContinuationToken?)"/> and treat a failed parse like an
    /// <see cref="InvalidContinuationTokenException"/> (for example, a malformed cursor).
    /// </summary>
    public const string Beginning = "Beginning";
}
