// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying.SearchFields;

/// <summary>
/// Shared limits for indexed search field values.
/// </summary>
internal static class SearchFieldValueLimits
{
    /// <summary>
    /// The maximum length, in characters, of a string value that can be indexed as a search field.
    /// </summary>
    /// <remarks>
    /// Matches SQL Server's <c>NVARCHAR(500)</c> and Oracle's <c>NVARCHAR2(500)</c> column size for
    /// <c>string_value</c>, so values beyond this length already fail at the database on those
    /// providers. SQLite and PostgreSQL store it as unbounded <c>TEXT</c>, so this limit is enforced
    /// in code for all providers, giving identical behavior everywhere, and is intentionally not tied
    /// to <see cref="Duende.Storage.Internal.Querying.SearchExpression.MaxLength"/> (which bounds filter literals,
    /// not indexed values). It also bounds continuation tokens: see <see cref="Cursors.CursorToken"/>,
    /// which is sized so a string value at this length always round-trips through <c>Encode</c>/<c>Decode</c>.
    /// </remarks>
    internal const int MaxStringValueLength = 500;
}
