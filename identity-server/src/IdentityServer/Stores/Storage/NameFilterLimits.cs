// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Stores.Storage;

/// <summary>
/// Limits for by-name lookups that filter with <c>Fields.Name.In(...)</c>.
/// </summary>
internal static class NameFilterLimits
{
    /// <summary>
    /// The most names put into one <c>In</c> filter. Storage turns each name into a SQL parameter in a single
    /// <c>IN (...)</c> list, and Oracle allows at most 1,000 expressions in such a list (SQL Server about
    /// 2,100 parameters per command), so longer name lists are queried in chunks of this size.
    /// </summary>
    public const int MaxNamesPerQuery = 1000;
}
