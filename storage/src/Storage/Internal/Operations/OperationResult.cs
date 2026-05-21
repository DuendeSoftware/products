// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Operations;

/// <summary>
/// Represents the result of an individual operation within a batch.
/// </summary>
/// <param name="Index">The zero-based index of the operation in the batch.</param>
/// <param name="Outcome">The outcome of the operation.</param>
public sealed record OperationResult(int Index, OperationOutcome Outcome);
