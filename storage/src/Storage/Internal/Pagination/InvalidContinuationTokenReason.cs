// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Pagination;

/// <summary>
/// The reason a continuation token was rejected by <see cref="InvalidContinuationTokenException"/>.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
public enum InvalidContinuationTokenReason
{
    /// <summary>
    /// The token could not be decoded: it is not valid Base64Url, its length does not match any known
    /// layout, it has reserved flag bits set, or a value it carries is out of range.
    /// </summary>
    Malformed,

    /// <summary>
    /// The token's format version is not understood by this version of Duende Storage.
    /// </summary>
    UnsupportedVersion,

    /// <summary>
    /// The token's sort fingerprint (entity type, field, field kind and direction) does not match the
    /// query it was supplied with.
    /// </summary>
    SortMismatch
}
