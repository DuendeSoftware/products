// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue.Internal.Storage;

public static class AttributeGroupDso
{
    public sealed record V1(string Code, string? DisplayName, string? Description, int Order);
}
