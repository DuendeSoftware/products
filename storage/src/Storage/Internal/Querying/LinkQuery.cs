// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying;

/// <summary>
/// Fluent entry point for building link traversal queries.
/// </summary>
/// <example>
/// <code>
/// var query = LinkQuery.From(userEntityType)
///     .Join(UserRoleDefinition)
///     .Where(roleEntityType, roleId)
///     .Build();
/// </code>
/// </example>
public static class LinkQuery
{
    /// <summary>
    /// Starts a new link query from the given source entity type.
    /// </summary>
    public static LinkQueryBuilder From(EntityType source) => new(source);
}
