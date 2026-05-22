// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Operations;

/// <summary>
/// Represents the result of an individual operation within a batch.
/// </summary>
/// <remarks>
/// This type is for usage by Duende Software products, is not supported for end user consumption, and not subject to semantic versioning rules.
/// </remarks>
/// <param name="Index">The zero-based index of the operation in the batch.</param>
/// <param name="Outcome">The outcome of the operation.</param>
public sealed record OperationResult(int Index, OperationOutcome Outcome);
