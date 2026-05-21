// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue.Internal.Storage;

public static class AttributeDefinitionDso
{
    public sealed record V1(
        string Code,
        AttributeTypeDso Type,
        string? Description,
        bool IsUnique,
        IReadOnlyList<string> Tags,
        string? GroupCode,
        int Order,
        string? DisplayName);
}
