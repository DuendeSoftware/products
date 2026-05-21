// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Operations;

public enum UpdateResult
{
    Success,
    DoesNotExist,
    UnexpectedVersion,
    KeyConflict
}
