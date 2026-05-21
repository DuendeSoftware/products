// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Filtering;

internal readonly record struct LexToken(FilterToken Type, string Value, int Position)
{
    [Obsolete("Don't use parameterless constructor")]
    public LexToken() : this(default, string.Empty, 0) =>
        throw new InvalidOperationException("Don't use parameterless constructor");
}
