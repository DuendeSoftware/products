// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Filtering.Expressions;

public sealed class AttributePathExpression(string path) : FilterExpression
{
    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));

    public override string ToString() => Path;
}
