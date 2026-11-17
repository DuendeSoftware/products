// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Models;

/// <summary>
/// An opaque continuation token used for cursor-based pagination.
/// Returned in <see cref="QueryResult{T}"/> and consumed by <see cref="DataRange"/>.
/// The token's contents are an implementation detail: callers must not parse or construct one by
/// hand, only pass back a token they previously received, unchanged, with the same filter and sort.
/// </summary>
[StringValue]
public partial record ContinuationToken
{
    /// <summary>
    /// The token value representing the beginning of the dataset. Using it starts at the first page.
    /// An empty string is not a valid token on its own: <c>ContinuationToken.Create("")</c> throws
    /// <see cref="FormatException"/>. To start from the first page, use <see cref="Beginning"/>
    /// instead.
    /// </summary>
    public const string Beginning = "Beginning";
}
